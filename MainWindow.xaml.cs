using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Neo.IronLua;
using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;

namespace BAGMON
{
    public class Server
    {
        public string Name { get; set; }
        public LuaTable Data { get; set; }
    }

    public class Character
    {
        public string Name { get; set; }
        public int Money { get; set; }
        public string Class { get; set; }
        public string Guild { get; set; }
        public string Race { get; set; }
        public bool Faction { get; set; }
        public int Sex { get; set; }

        public LuaTable Equip { get; set; }

        public LuaTable Keychain { get; set; } // -2
        public LuaTable Backpack0 { get; set; } // 0
        public LuaTable Backpack1 { get; set; } // 1
        public LuaTable Backpack2 { get; set; } // 2
        public LuaTable Backpack3 { get; set; } // 3
        public LuaTable Backpack4 { get; set; } // 4

        public LuaTable Bank0 { get; set; } // -1
        public LuaTable Bank1 { get; set; } // 5
        public LuaTable Bank2 { get; set; } // 6
        public LuaTable Bank3 { get; set; } // 7
        public LuaTable Bank4 { get; set; } // 8
        public LuaTable Bank5 { get; set; } // 9
        public LuaTable Bank6 { get; set; } // 10
        public LuaTable Bank7 { get; set; } // 11
        public LuaTable Bank8 { get; set; } // 12

        public LuaTable Mailbox { get; set; } // mailbox

    }

    public class Item
    {
        public string Container { get; set; }
        public LinkItem ItemLink { get; set; }
        public int Slot { get; set; }
        public int Quantity { get; set; }
        public string CharacterName { get; set; }
        public int ItemId { get; set; }
    }

    public class LinkItem
    {

        // e.g. "12926::::::1024:320883200:60:::1::::",
        // "19682:1893:::::::60:::::::", -- [5]
        public int ItemID { get; set; } // 12926
        public int EnchantID { get; set; } // 2nd field
        public int SuffixID { get; set; } // 1024
        // public int UniqueID { get; set; } // 320883200 ? useless?
        public int Quality { get; set; }

        private static Dictionary<int, string> cacheName = new Dictionary<int, string>();
        private static Dictionary<int, int> cacheQuality = new Dictionary<int, int>();

        public string name { get; set; }
        public LinkItem(string link, SqliteConnection database)
        {
            string[] parts = link.Split(":");
            this.ItemID = int.Parse(parts[0]);
            if (parts[1].Length > 0)
            {
                this.EnchantID = int.Parse(parts[1]);
            }
            if (parts[6].Length > 0)
            {
                this.SuffixID = int.Parse(parts[6]);
            }

            if (cacheName.TryGetValue(this.ItemID, out var n))
            {
                name = n;
                Quality = cacheQuality[this.ItemID];

            } else
            {
                // The Schema:
                //CREATE TABLE `item_template` (
                //  `name` varchar(255) NOT NULL DEFAULT '',
                //  `Quality` INTEGER NOT NULL DEFAULT '0',
                //  `entry` INTEGER NOT NULL DEFAULT '0'
                // )
                // PRIMARY KEY(`entry`)

                var command = database.CreateCommand();
                command.CommandText =
                @"
                SELECT name, Quality FROM item_template WHERE entry = $id
                ";
                command.Parameters.AddWithValue("$id", this.ItemID);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        name = reader.GetString(0);
                        Quality = reader.GetInt32(1);
                    }
                }

                cacheName[ItemID] = name;
                cacheQuality[ItemID] = Quality;

            }

        }

        public override string ToString()
        {
            return $"{this.name.Replace("\\","")}";
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Server> Servers { get; set; }
        public ObservableCollection<Character> Characters { get; set; }
        public ObservableCollection<Item> Items { get; set; }

        private SqliteConnection database = new SqliteConnection("Data Source=items.db");

        private Server _selectedServer;
        public Server SelectedServer {
            get {
                return _selectedServer;
            }
            set
            {
                _selectedServer = value;
                Characters.Clear();
                if (value == null)
                {
                    return;
                }
                foreach (KeyValuePair<object, object> character in value.Data)
                {
                    LuaTable values = (LuaTable)character.Value;
                    Characters.Add(new Character()
                    {
                        Name = (string)character.Key,
                        Guild = (string)values["guild"],
                        Money = (int)values["money"],
                        Class = (string)values["class"],
                        Race = (string)values["race"],
                        Faction = (bool)values["faction"],
                        Sex = (int)values["sex"],

                        Mailbox = (LuaTable)values["mailbox"],
                        Equip = (LuaTable)values["equip"],
                        Keychain = (LuaTable)values[-2],

                        Backpack0 = (LuaTable)values[0],
                        Backpack1 = (LuaTable)values[1],
                        Backpack2 = (LuaTable)values[2],
                        Backpack3 = (LuaTable)values[3],
                        Backpack4 = (LuaTable)values[4],

                        Bank0 = (LuaTable)values[-1],
                        Bank1 = (LuaTable)values[5],
                        Bank2 = (LuaTable)values[6],
                        Bank3 = (LuaTable)values[7],
                        Bank4 = (LuaTable)values[8],
                        Bank5 = (LuaTable)values[9],
                        Bank6 = (LuaTable)values[10],
                        Bank7 = (LuaTable)values[11],
                        Bank8 = (LuaTable)values[12],

                    });
                }
                ReloadUI();
            }
        }
        private Character _selectedCharacter;
        public Character SelectedCharacter
        {
            get
            {
                return _selectedCharacter;
            }
            set
            {
                _selectedCharacter = value;
                ReloadUI();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Servers = new ObservableCollection<Server>();
            Characters = new ObservableCollection<Character>();
            Items = new ObservableCollection<Item>();

            database.Open();

            Refresh();

        }

        public void Refresh()
        {
            string[] accounts = Directory.GetDirectories("C:\\Program Files (x86)\\World of Warcraft\\_classic_\\WTF\\Account");
            Servers.Clear();
            foreach (string account in accounts)
            {
                using (Lua lua = new Lua()) // Create the Lua script engine
                {
                    LuaGlobal env = lua.CreateEnvironment();
                    string filepath = $"{account}\\SavedVariables\\BagBrother.lua";
                    if (File.Exists(filepath))
                    {
                        string inputFile = File.ReadAllText(filepath);
                        env.DoChunk(inputFile, "test.lua");
                        LuaResult r = env.DoChunk("return BrotherBags;", "test.lua");
                        LuaTable luaservers = (LuaTable)r[0];
                        foreach (KeyValuePair<object, object> server in luaservers)
                        {
                            var serverName = (string)server.Key;
                            var characters = (LuaTable)server.Value;
                            var existingServer = Servers.Where((x) => x.Name == serverName);
                            if (existingServer.Any())
                            {
                                var s = existingServer.First();
                                foreach (var x in characters)
                                {
                                    s.Data[x.Key] = x.Value;
                                }
                            }
                            else
                            {
                                Servers.Add(new Server()
                                {
                                    Name = serverName,
                                    Data = characters
                                });
                            }
                        }
                    }
                    
                }
            }
            
        }

        public void ReloadUI()
        {
            Items.Clear();
            if (this.SelectedCharacter == null)
            {
                // show everthing.
                CharName.Content = "";
                foreach (Character character in Characters)
                {
                    AddItems(character);
                }
                CharSelect.SelectedIndex = -1;

            } else
            {
                CharName.Content = this.SelectedCharacter.Class;
                AddItems(this.SelectedCharacter);
            }
            
        }

        private void AddBagItems(string container, string charName, LuaTable items)
        {
            if (items == null)
            {
                return;
            }
            foreach (KeyValuePair<object, object> item in items)
            {
                if (item.Key is string || item.Value == null)
                {
                    // skip the 'size' key.
                } else
                {
                    string data = (string)item.Value;
                    string[] stringData = data.Split(";");
                    int quantity = 1;
                    if (stringData.Length > 1)
                    {
                        quantity = int.Parse(stringData[1]);
                    }
                    string itemLink = stringData[0];
                    var link = new LinkItem(itemLink, database);
                    var x = new Item()
                    {
                        Container = container,
                        Slot = (int)item.Key,
                        Quantity = quantity,
                        CharacterName = charName,
                        ItemId = link.ItemID,
                        ItemLink = link
                    };
                    if (SearchFilter.Text.Length == 0 || x.ItemLink.name.ToLower().Contains(SearchFilter.Text.ToLower()))
                    {
                        Items.Add(x);
                    }
                }
                
            }
            
        }

        private void AddItems(Character selectedCharacter)
        {
            AddBagItems("Mailbox", selectedCharacter.Name, selectedCharacter.Mailbox);
            AddBagItems("Equip", selectedCharacter.Name, selectedCharacter.Equip);
            AddBagItems("Keychain", selectedCharacter.Name, selectedCharacter.Keychain);
            AddBagItems("Backpack0", selectedCharacter.Name, selectedCharacter.Backpack0);
            AddBagItems("Backpack1", selectedCharacter.Name, selectedCharacter.Backpack1);
            AddBagItems("Backpack2", selectedCharacter.Name, selectedCharacter.Backpack2);
            AddBagItems("Backpack3", selectedCharacter.Name, selectedCharacter.Backpack3);
            AddBagItems("Backpack4", selectedCharacter.Name, selectedCharacter.Backpack4);
            AddBagItems("Bank0", selectedCharacter.Name, selectedCharacter.Bank0);
            AddBagItems("Bank1", selectedCharacter.Name, selectedCharacter.Bank1);
            AddBagItems("Bank2", selectedCharacter.Name, selectedCharacter.Bank2);
            AddBagItems("Bank3", selectedCharacter.Name, selectedCharacter.Bank3);
            AddBagItems("Bank4", selectedCharacter.Name, selectedCharacter.Bank4);
            AddBagItems("Bank5", selectedCharacter.Name, selectedCharacter.Bank5);
            AddBagItems("Bank6", selectedCharacter.Name, selectedCharacter.Bank6);
            AddBagItems("Bank7", selectedCharacter.Name, selectedCharacter.Bank7);
            AddBagItems("Bank8", selectedCharacter.Name, selectedCharacter.Bank8);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.SelectedCharacter = null;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReloadUI();
        }
    }
}
