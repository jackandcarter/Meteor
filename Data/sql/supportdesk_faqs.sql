/*
MySQL Data Transfer
Source Host: localhost
Source Database: ffxiv_server
Target Host: localhost
Target Database: ffxiv_server
Date: 8/20/2016 7:15:35 PM
*/

SET FOREIGN_KEY_CHECKS=0;
-- ----------------------------
-- Table structure for supportdesk_faqs
-- ----------------------------
CREATE TABLE `supportdesk_faqs` (
  `slot` tinyint(4) NOT NULL,
  `languageCode` tinyint(4) NOT NULL,
  `title` varchar(128) NOT NULL,
  `body` text NOT NULL,
  PRIMARY KEY (`slot`,`languageCode`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- ----------------------------
-- Records 
-- ----------------------------
INSERT INTO `supportdesk_faqs` VALUES ('0', '1', 'Welcome to FFXIV Classic', 'Welcome to AetherXIV Core for FFXIV 1.x.\r\n\r\nThis server is under active restoration, and testers may encounter incomplete content or protocol issues while playing.\r\n\r\nUse launcher news and project documentation for current development notes.');
