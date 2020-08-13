# BAGMON

How to use BAGMON:

1. Install the improved BagBrother addon to include Mailbox items in save data.
2. Find a sqlite3 database file so that the app can work out what the name of every item is. Put that file in this directory and name it items.db.
3. Data is updated when you log out of your character. Log out then press Refresh to search the latest data.

The `items.db` sqlite3 database should have the following schema:

	CREATE TABLE `item_template` (
		`name` varchar(255) NOT NULL DEFAULT '',
		`Quality` INTEGER NOT NULL DEFAULT '0',
		`entry` INTEGER NOT NULL DEFAULT '0',
		PRIMARY KEY(`entry`)
	)

This schema should be compatible with the data from https://github.com/cmangos/classic-db.
