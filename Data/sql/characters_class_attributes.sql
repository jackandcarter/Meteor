-- MySQL dump 10.13  Distrib 5.7.10, for Win64 (x86_64)
--
-- Host: localhost    Database: ffxiv_database
-- ------------------------------------------------------
-- Server version	5.7.10-log

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `characters_class_attributes`
--
-- FFXIV 1.20+ attribute allotment is class-scoped for Disciples of War/Magic.
-- Jobs inherit the associated class allotment rather than owning a separate pool.

DROP TABLE IF EXISTS `characters_class_attributes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `characters_class_attributes` (
  `characterId` int(10) unsigned NOT NULL,
  `classId` tinyint(3) unsigned NOT NULL,
  `pointsRemaining` smallint(6) NOT NULL DEFAULT '0',
  `strSpent` smallint(6) NOT NULL DEFAULT '0',
  `vitSpent` smallint(6) NOT NULL DEFAULT '0',
  `dexSpent` smallint(6) NOT NULL DEFAULT '0',
  `intSpent` smallint(6) NOT NULL DEFAULT '0',
  `mndSpent` smallint(6) NOT NULL DEFAULT '0',
  `pieSpent` smallint(6) NOT NULL DEFAULT '0',
  PRIMARY KEY (`characterId`,`classId`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `characters_class_attributes`
--

LOCK TABLES `characters_class_attributes` WRITE;
/*!40000 ALTER TABLE `characters_class_attributes` DISABLE KEYS */;
/*!40000 ALTER TABLE `characters_class_attributes` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-06-15 00:00:00
