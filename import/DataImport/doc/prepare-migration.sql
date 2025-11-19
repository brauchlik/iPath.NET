-- iPath encoding trouble. 
-- the mysql driver in php was set to latin1
-- the database encoding was set to utf-8
-- utf-8 data from user input was stored
-- 


-- DB encoding
SELECT default_character_set_name FROM information_schema.SCHEMATA 
WHERE schema_name = "ipath2";


-- Convert data and info field to proper utf-8 encoding
-- person
-- SELECT CONVERT(BINARY CONVERT(data USING latin1) USING utf8) AS convertedColumn FROM person where id=429;


-- allow mass updates
SET SQL_SAFE_UPDATES = 0;

-- update data and info field
UPDATE person SET data = CONVERT(BINARY CONVERT(data USING latin1) USING utf8);
UPDATE person SET info = CONVERT(BINARY CONVERT(info USING latin1) USING utf8);
UPDATE person
   SET username = CONVERT(BINARY CONVERT(username USING latin1) USING utf8)
     , email = CONVERT(BINARY CONVERT(email USING latin1) USING utf8);

-- SELECT data FROM person WHERE id = 429;

-- annotations
UPDATE annotation SET data = CONVERT(BINARY CONVERT(data USING latin1) USING utf8);
UPDATE annotation SET info = CONVERT(BINARY CONVERT(info USING latin1) USING utf8);

-- community
UPDATE community
   SET name = CONVERT(BINARY CONVERT(name USING latin1) USING utf8)
     , description = CONVERT(BINARY CONVERT(description USING latin1) USING utf8);
-- select * from community;

-- groups
UPDATE groups SET info = CONVERT(BINARY CONVERT(info USING latin1) USING utf8);
UPDATE groups SET name = CONVERT(BINARY CONVERT(name USING latin1) USING utf8);
-- select * from groups;

-- objects
UPDATE objects SET data = CONVERT(BINARY CONVERT(data USING latin1) USING utf8);
UPDATE objects SET info = CONVERT(BINARY CONVERT(info USING latin1) USING utf8);
-- select * from objects limit 100;



/*
*  Prepare the community and group tables for data transfer
*
***************************************************************************************/

-- community_group mneeds a primary key
ALTER TABLE community_group ADD id INT UNSIGNED PRIMARY KEY AUTO_INCREMENT;

-- remove non-existing relations between group and person
-- SELECT * FROM group_member WHERE user_id NOT IN (SELECT id FROM person);
-- SELECT * FROM group_member WHERE group_id NOT IN (SELECT id FROM groups);
DELETE FROM group_member WHERE user_id NOT IN (SELECT id FROM person);
DELETE FROM group_member WHERE group_id NOT IN (SELECT id FROM groups);

-- SELECT * from community_group where community_id NOT IN (SELECT id from community);
-- SELECT * from community_group where group_id NOT IN (SELECT id from groups);
DELETE from community_group where group_id NOT IN (SELECT id from groups);


-- added a storageId Field to groups and nodes to sync with G-Workspace
ALTER TABLE groups ADD storageId VARCHAR(40);

-- select * from groups;






/*
*
* prepare the parent child relations
*
*******************************************************************************/

ALTER TABLE objects ADD _top_id INT,
	 ADD _exportTime DATETIME null,
	 ADD storageId VARCHAR(40);
CREATE INDEX idx_objects_prarentandtop ON objects (parent_id, _top_id);

SET SQL_SAFE_UPDATES = 0;
UPDATE objects SET _top_id = NULL;

-- sanitize parent_id and group_id fields
-- parent_id should be NULL or > 0 and it should point to a valid parent.
-- group_id should be set only for parent_id where IS NULL and it should be NULL or > 0 and point to a valid group
-- we need this for EF Core to understand the relations
-----------------------------------------------------------------------------------------------------------------
UPDATE objects SET parent_id = NULL WHERE parent_id = 0;
UPDATE objects SET parent_id = NULL WHERE parent_id = -1;
-- check 
select * from objects where NOT class='imic' and NOT parent_id IS NULL AND NOT parent_id IN (SELECT id from objects);

-- udpate groups
UPDATE objects SET group_id = NULL WHERE group_id = 0;
-- check 
-- select * from objects where group_id < 0;
UPDATE objects SET group_id = NULL WHERE group_id < 0;

/*
select count(*)
  from objects c
  join objects p on p.id = c.parent_id
 where p.parent_id IS NULL AND p.group_id > 0;
 */

-- LEVEL 1: direct child nodes of top-parent. Top-parent has no parent and belongs to a group (parent_id IS NULL and group_id > 0)
--  part 1
UPDATE objects child
  JOIN objects parent ON parent.id = child.parent_id
   SET child._top_id = parent.id
 WHERE child.id < 1000000
   AND parent.parent_id IS NULL AND parent.group_id > 0;

-- LEVEL 1: part 2:
UPDATE objects child
  JOIN objects parent ON parent.id = child.parent_id
   SET child._top_id = parent.id
 WHERE child.id >= 1000000
   AND parent.parent_id IS NULL AND parent.group_id > 0;
      

-- LEVEL n: child of child -> use _top_id from parent => REPEAT until no objects is changed anymore
UPDATE objects child
  JOIN objects parent ON parent.id = child.parent_id
   SET child._top_id = parent._top_id
 WHERE NOT parent._top_id IS NULL
   AND child._top_id IS NULL;

