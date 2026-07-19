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
-- Table structure for table `server_player_base_stats`
--
-- Player base stats are intentionally data-only. Patch 1.19/1.21 prove that
-- class/job and level control automatic base attributes, and that jobs can
-- have different base attributes while sharing base-class level/EXP. Rows in
-- this table must come from client extraction, retail traces, or archived
-- source evidence; use tribe 0 for all-tribe rows until tribe-specific values
-- are confirmed.

DROP TABLE IF EXISTS `server_player_base_stats`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `server_player_base_stats` (
  `classId` tinyint(3) unsigned NOT NULL,
  `tribe` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `level` smallint(6) NOT NULL,
  `hp` smallint(6) NOT NULL DEFAULT '0',
  `mp` smallint(6) NOT NULL DEFAULT '0',
  `str` smallint(6) NOT NULL DEFAULT '0',
  `vit` smallint(6) NOT NULL DEFAULT '0',
  `dex` smallint(6) NOT NULL DEFAULT '0',
  `int` smallint(6) NOT NULL DEFAULT '0',
  `mnd` smallint(6) NOT NULL DEFAULT '0',
  `pie` smallint(6) NOT NULL DEFAULT '0',
  `source` varchar(255) DEFAULT NULL,
  `sourceConfidence` enum('client-confirmed','trace-confirmed','public-confirmed','hypothesis') NOT NULL DEFAULT 'hypothesis',
  PRIMARY KEY (`classId`,`tribe`,`level`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `server_player_base_stats`
--
-- No seed rows are provided here because base stat values still need direct
-- 1.23b client/trace confirmation.

LOCK TABLES `server_player_base_stats` WRITE;
/*!40000 ALTER TABLE `server_player_base_stats` DISABLE KEYS */;
/*!40000 ALTER TABLE `server_player_base_stats` ENABLE KEYS */;
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
