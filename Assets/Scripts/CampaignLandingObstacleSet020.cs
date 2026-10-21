namespace OdysseyOracle
{
    // PSEUDOCODE: freeze provider records by semantic identity; exclude key302;
    // sort(Morton,Key) once offline; dispatch directly to each unchanged provider.
    public static class CampaignLandingObstacleSet020
    {
        public const uint Revision=1;
        public const int Count=162;
        public static bool IsCollisionOnly(in LandingBox020 value)=>
            CampaignTavernLayout020.IsObstacle(value)||CampaignPlantedForecourtRecipe020.IsPlanterBody(value)||
            (value.Kind==9&&value.Key>=7000u&&value.Key<7008u);
        public static bool TryObstacle(int index,out LandingBox020 value)
        {
            switch(index)
            {
                case 0:return PortalLandingLayout020.TryOriginalObstacle020(0,out value); // key1025, Morton112446
                case 1:return PortalLandingLayout020.TryOriginalObstacle020(1,out value); // key1023, Morton113813
                case 2:return PortalLandingLayout020.TryOriginalObstacle020(2,out value); // key1002, Morton114234
                case 3:return PortalLandingLayout020.TryOriginalObstacle020(3,out value); // key1021, Morton117742
                case 4:return PortalLandingLayout020.TryOriginalObstacle020(4,out value); // key1016, Morton118485
                case 5:return PortalLandingLayout020.TryOriginalObstacle020(5,out value); // key1019, Morton123045
                case 6:return CampaignPlantedForecourtRecipe020.TryPlanterBody(6,out value); // key6006, Morton130675
                case 7:return PortalLandingLayout020.TryOriginalObstacle020(6,out value); // key1000, Morton201173
                case 8:return PortalLandingLayout020.TryOriginalObstacle020(7,out value); // key1003, Morton202388
                case 9:return PortalLandingLayout020.TryOriginalObstacle020(8,out value); // key1028, Morton203973
                case 10:return PortalLandingLayout020.TryOriginalObstacle020(9,out value); // key1031, Morton204740
                case 11:return CampaignPlantedForecourtRecipe020.TryPlanterBody(5,out value); // key6005, Morton218841
                case 12:return PortalLandingLayout020.TryOriginalObstacle020(10,out value); // key1033, Morton221237
                case 13:return PortalLandingLayout020.TryOriginalObstacle020(11,out value); // key1035, Morton222800
                case 14:return PortalLandingLayout020.TryOriginalObstacle020(12,out value); // key1064, Morton225909
                case 15:return PortalLandingLayout020.TryOriginalObstacle020(13,out value); // key1065, Morton226037
                case 16:return PortalLandingLayout020.TryOriginalObstacle020(14,out value); // key1066, Morton227445
                case 17:return PortalLandingLayout020.TryOriginalObstacle020(15,out value); // key1067, Morton227573
                case 18:return PortalLandingLayout020.TryOriginalObstacle020(16,out value); // key1068, Morton227957
                case 19:return PortalLandingLayout020.TryOriginalObstacle020(17,out value); // key1069, Morton228085
                case 20:return PortalLandingLayout020.TryOriginalObstacle020(18,out value); // key1004, Morton230197
                case 21:return PortalLandingLayout020.TryOriginalObstacle020(19,out value); // key1009, Morton230330
                case 22:return PortalLandingLayout020.TryOriginalObstacle020(20,out value); // key1007, Morton231637
                case 23:return PortalLandingLayout020.TryOriginalObstacle020(21,out value); // key1006, Morton233594
                case 24:return PortalLandingLayout020.TryOriginalObstacle020(22,out value); // key303, Morton236240
                case 25:return CampaignPlantedForecourtRecipe020.TryPlanterBody(3,out value); // key6003, Morton236658
                case 26:return PortalLandingLayout020.TryOriginalObstacle020(23,out value); // key1037, Morton237653
                case 27:return PortalLandingLayout020.TryOriginalObstacle020(24,out value); // key1013, Morton237818
                case 28:return PortalLandingLayout020.TryOriginalObstacle020(25,out value); // key1011, Morton239637
                case 29:return PortalLandingLayout020.TryOriginalObstacle020(26,out value); // key1015, Morton240208
                case 30:return CampaignPlantedForecourtRecipe020.TryPlanterBody(2,out value); // key6002, Morton242904
                case 31:return PortalLandingLayout020.TryOriginalObstacle020(27,out value); // key1005, Morton246304
                case 32:return PortalLandingLayout020.TryOriginalObstacle020(28,out value); // key1010, Morton246458
                case 33:return PortalLandingLayout020.TryOriginalObstacle020(29,out value); // key1008, Morton248000
                case 34:return PortalLandingLayout020.TryOriginalObstacle020(30,out value); // key1039, Morton249082
                case 35:return PortalLandingLayout020.TryOriginalObstacle020(31,out value); // key1070, Morton249973
                case 36:return PortalLandingLayout020.TryOriginalObstacle020(32,out value); // key1071, Morton250101
                case 37:return PortalLandingLayout020.TryOriginalObstacle020(33,out value); // key1072, Morton250485
                case 38:return PortalLandingLayout020.TryOriginalObstacle020(34,out value); // key1073, Morton250613
                case 39:return PortalLandingLayout020.TryOriginalObstacle020(35,out value); // key1074, Morton252021
                case 40:return PortalLandingLayout020.TryOriginalObstacle020(36,out value); // key1075, Morton252149
                case 41:return PortalLandingLayout020.TryOriginalObstacle020(37,out value); // key301, Morton254681
                case 42:return PortalLandingLayout020.TryOriginalObstacle020(38,out value); // key1018, Morton297146
                case 43:return PortalLandingLayout020.TryOriginalObstacle020(39,out value); // key1017, Morton302016
                case 44:return PortalLandingLayout020.TryOriginalObstacle020(40,out value); // key1022, Morton302842
                case 45:return CampaignPlantedForecourtRecipe020.TryPlanterBody(7,out value); // key6007, Morton305958
                case 46:return PortalLandingLayout020.TryOriginalObstacle020(41,out value); // key1020, Morton319648
                case 47:return PortalLandingLayout020.TryOriginalObstacle020(42,out value); // key1026, Morton320298
                case 48:return PortalLandingLayout020.TryOriginalObstacle020(43,out value); // key1027, Morton322516
                case 49:return PortalLandingLayout020.TryOriginalObstacle020(44,out value); // key1024, Morton322688
                case 50:return CampaignPlantedForecourtRecipe020.TryPlanterBody(4,out value); // key6004, Morton394124
                case 51:return PortalLandingLayout020.TryOriginalObstacle020(45,out value); // key1076, Morton403232
                case 52:return PortalLandingLayout020.TryOriginalObstacle020(46,out value); // key1077, Morton403360
                case 53:return PortalLandingLayout020.TryOriginalObstacle020(47,out value); // key1078, Morton404768
                case 54:return PortalLandingLayout020.TryOriginalObstacle020(48,out value); // key1079, Morton404896
                case 55:return PortalLandingLayout020.TryOriginalObstacle020(49,out value); // key1080, Morton405280
                case 56:return PortalLandingLayout020.TryOriginalObstacle020(50,out value); // key1081, Morton405408
                case 57:return PortalLandingLayout020.TryOriginalObstacle020(51,out value); // key1036, Morton406336
                case 58:return PortalLandingLayout020.TryOriginalObstacle020(52,out value); // key1001, Morton410064
                case 59:return PortalLandingLayout020.TryOriginalObstacle020(53,out value); // key1030, Morton410494
                case 60:return PortalLandingLayout020.TryOriginalObstacle020(54,out value); // key1032, Morton412544
                case 61:return PortalLandingLayout020.TryOriginalObstacle020(55,out value); // key1029, Morton412800
                case 62:return PortalLandingLayout020.TryOriginalObstacle020(56,out value); // key1034, Morton417824
                case 63:return PortalLandingLayout020.TryOriginalObstacle020(57,out value); // key1043, Morton424554
                case 64:return PortalLandingLayout020.TryOriginalObstacle020(58,out value); // key1082, Morton427296
                case 65:return PortalLandingLayout020.TryOriginalObstacle020(59,out value); // key1083, Morton427424
                case 66:return PortalLandingLayout020.TryOriginalObstacle020(60,out value); // key1084, Morton427808
                case 67:return PortalLandingLayout020.TryOriginalObstacle020(61,out value); // key1085, Morton427936
                case 68:return PortalLandingLayout020.TryOriginalObstacle020(62,out value); // key1086, Morton429344
                case 69:return PortalLandingLayout020.TryOriginalObstacle020(63,out value); // key1087, Morton429472
                case 70:return PortalLandingLayout020.TryOriginalObstacle020(64,out value); // key1040, Morton433386
                case 71:return PortalLandingLayout020.TryOriginalObstacle020(65,out value); // key305, Morton434816
                case 72:return PortalLandingLayout020.TryOriginalObstacle020(66,out value); // key1041, Morton443253
                case 73:return PortalLandingLayout020.TryOriginalObstacle020(67,out value); // key1042, Morton447880
                case 74:return PortalLandingLayout020.TryOriginalObstacle020(68,out value); // key1038, Morton456072
                case 75:return PortalLandingLayout020.TryOriginalObstacle020(69,out value); // key1014, Morton456110
                case 76:return PortalLandingLayout020.TryOriginalObstacle020(70,out value); // key1012, Morton458000
                case 77:return PortalLandingLayout020.TryOriginalObstacle020(71,out value); // key1050, Morton458174
                case 78:return PortalLandingLayout020.TryOriginalObstacle020(72,out value); // key4007, Morton492179
                case 79:return PortalLandingLayout020.TryOriginalObstacle020(73,out value); // key1088, Morton492724
                case 80:return PortalLandingLayout020.TryOriginalObstacle020(74,out value); // key1089, Morton495364
                case 81:return PortalLandingLayout020.TryOriginalObstacle020(75,out value); // key229, Morton496795
                case 82:return PortalLandingLayout020.TryOriginalObstacle020(76,out value); // key228, Morton496852
                case 83:return PortalLandingLayout020.TryOriginalObstacle020(77,out value); // key208, Morton551723
                case 84:return PortalLandingLayout020.TryOriginalObstacle020(78,out value); // key209, Morton551780
                case 85:return PortalLandingLayout020.TryOriginalObstacle020(79,out value); // key1090, Morton553211
                case 86:return PortalLandingLayout020.TryOriginalObstacle020(80,out value); // key1091, Morton555851
                case 87:return PortalLandingLayout020.TryOriginalObstacle020(81,out value); // key4006, Morton556396
                case 88:return PortalLandingLayout020.TryOriginalObstacle020(82,out value); // key1048, Morton589909
                case 89:return PortalLandingLayout020.TryOriginalObstacle020(83,out value); // key1052, Morton591989
                case 90:return PortalLandingLayout020.TryOriginalObstacle020(84,out value); // key1055, Morton592599
                case 91:return PortalLandingLayout020.TryOriginalObstacle020(85,out value); // key1044, Morton600311
                case 92:return PortalLandingLayout020.TryOriginalObstacle020(86,out value); // key1045, Morton601040
                case 93:return CampaignPlantedForecourtRecipe020.TryPlanterBody(1,out value); // key6001, Morton615069
                case 94:return CampaignPlantedForecourtRecipe020.TryPlanterBody(0,out value); // key6000, Morton616328
                case 95:return CampaignTavernLayout020.TryObstacle(3,out value); // key8003, Morton616539
                case 96:return CampaignTavernLayout020.TryObstacle(6,out value); // key8006, Morton616734
                case 97:return CampaignTavernLayout020.TryObstacle(9,out value); // key8009, Morton616766
                case 98:return CampaignTavernLayout020.TryObstacle(5,out value); // key8005, Morton616794
                case 99:return CampaignTavernLayout020.TryObstacle(8,out value); // key8008, Morton616794
                case 100:return CampaignTavernLayout020.TryObstacle(14,out value); // key8014, Morton616989
                case 101:return CampaignTavernLayout020.TryObstacle(12,out value); // key8012, Morton617049
                case 102:return CampaignTavernLayout020.TryObstacle(0,out value); // key8000, Morton617092
                case 103:return CampaignTavernLayout020.TryObstacle(15,out value); // key8015, Morton617240
                case 104:return CampaignTavernLayout020.TryObstacle(10,out value); // key8010, Morton617424
                case 105:return CampaignTavernLayout020.TryObstacle(7,out value); // key8007, Morton617483
                case 106:return CampaignTavernLayout020.TryObstacle(4,out value); // key8004, Morton617550
                case 107:return CampaignTavernLayout020.TryObstacle(11,out value); // key8011, Morton618220
                case 108:return CampaignTavernLayout020.TryObstacle(1,out value); // key8001, Morton618385
                case 109:return PortalLandingLayout020.TryOriginalObstacle020(88,out value); // key4000, Morton618988
                case 110:return PortalLandingLayout020.TryOriginalObstacle020(89,out value); // key1092, Morton619294
                case 111:return PortalLandingLayout020.TryOriginalObstacle020(90,out value); // key1093, Morton619414
                case 112:return PortalLandingLayout020.TryOriginalObstacle020(91,out value); // key1094, Morton619454
                case 113:return PortalLandingLayout020.TryOriginalObstacle020(92,out value); // key1095, Morton620854
                case 114:return PortalLandingLayout020.TryOriginalObstacle020(93,out value); // key1096, Morton620958
                case 115:return PortalLandingLayout020.TryOriginalObstacle020(94,out value); // key1097, Morton621334
                case 116:return PortalLandingLayout020.TryOriginalObstacle020(95,out value); // key1098, Morton621374
                case 117:return PortalLandingLayout020.TryOriginalObstacle020(96,out value); // key4002, Morton621412
                case 118:return PortalLandingLayout020.TryOriginalObstacle020(97,out value); // key1099, Morton621494
                case 119:return PortalLandingLayout020.TryOriginalObstacle020(98,out value); // key1046, Morton623765
                case 120:return CampaignTavernLayout020.TryObstacle(16,out value); // key8016, Morton638999
                case 121:return CampaignTavernLayout020.TryObstacle(13,out value); // key8013, Morton639059
                case 122:return CampaignTavernLayout020.TryObstacle(17,out value); // key8017, Morton639250
                case 123:return CampaignTavernLayout020.TryObstacle(2,out value); // key8002, Morton639474
                case 124:return PortalLandingLayout020.TryOriginalObstacle020(99,out value); // key1047, Morton639616
                case 125:return PortalLandingLayout020.TryOriginalObstacle020(100,out value); // key1100, Morton643358
                case 126:return PortalLandingLayout020.TryOriginalObstacle020(101,out value); // key1101, Morton643478
                case 127:return PortalLandingLayout020.TryOriginalObstacle020(102,out value); // key1102, Morton643518
                case 128:return PortalLandingLayout020.TryOriginalObstacle020(103,out value); // key1103, Morton643894
                case 129:return PortalLandingLayout020.TryOriginalObstacle020(104,out value); // key1059, Morton646037
                case 130:return PortalLandingLayout020.TryOriginalObstacle020(105,out value); // key4004, Morton652312
                case 131:return PortalLandingLayout020.TryOriginalObstacle020(106,out value); // key5000, Morton652399
                case 132:return PortalLandingLayout020.TryOriginalObstacle020(107,out value); // key306, Morton652432
                case 133:return PortalLandingLayout020.TryOriginalObstacle020(108,out value); // key4001, Morton795833
                case 134:return PortalLandingLayout020.TryOriginalObstacle020(109,out value); // key1104, Morton796235
                case 135:return PortalLandingLayout020.TryOriginalObstacle020(110,out value); // key1105, Morton796355
                case 136:return PortalLandingLayout020.TryOriginalObstacle020(111,out value); // key1106, Morton796395
                case 137:return PortalLandingLayout020.TryOriginalObstacle020(112,out value); // key1107, Morton797795
                case 138:return PortalLandingLayout020.TryOriginalObstacle020(113,out value); // key1108, Morton797899
                case 139:return PortalLandingLayout020.TryOriginalObstacle020(114,out value); // key4003, Morton798257
                case 140:return PortalLandingLayout020.TryOriginalObstacle020(115,out value); // key1109, Morton798275
                case 141:return PortalLandingLayout020.TryOriginalObstacle020(116,out value); // key1110, Morton798315
                case 142:return PortalLandingLayout020.TryOriginalObstacle020(117,out value); // key1111, Morton798435
                case 143:return PortalLandingLayout020.TryOriginalObstacle020(118,out value); // key304, Morton800400
                case 144:return PortalLandingLayout020.TryOriginalObstacle020(119,out value); // key1049, Morton808256
                case 145:return PortalLandingLayout020.TryOriginalObstacle020(120,out value); // key1051, Morton808720
                case 146:return PortalLandingLayout020.TryOriginalObstacle020(121,out value); // key1053, Morton810272
                case 147:return PortalLandingLayout020.TryOriginalObstacle020(122,out value); // key1054, Morton810708
                case 148:return PortalLandingLayout020.TryOriginalObstacle020(123,out value); // key1056, Morton816240
                case 149:return PortalLandingLayout020.TryOriginalObstacle020(124,out value); // key1057, Morton818288
                case 150:return PortalLandingLayout020.TryOriginalObstacle020(125,out value); // key1058, Morton818836
                case 151:return PortalLandingLayout020.TryOriginalObstacle020(126,out value); // key1112, Morton820299
                case 152:return PortalLandingLayout020.TryOriginalObstacle020(127,out value); // key1113, Morton820419
                case 153:return PortalLandingLayout020.TryOriginalObstacle020(128,out value); // key1114, Morton820459
                case 154:return PortalLandingLayout020.TryOriginalObstacle020(129,out value); // key1115, Morton820835
                case 155:return PortalLandingLayout020.TryOriginalObstacle020(130,out value); // key1060, Morton822976
                case 156:return PortalLandingLayout020.TryOriginalObstacle020(131,out value); // key5001, Morton827706
                case 157:return PortalLandingLayout020.TryOriginalObstacle020(132,out value); // key4005, Morton827725
                case 158:return PortalLandingLayout020.TryOriginalObstacle020(133,out value); // key307, Morton827845
                case 159:return PortalLandingLayout020.TryOriginalObstacle020(134,out value); // key1061, Morton828032
                case 160:return PortalLandingLayout020.TryOriginalObstacle020(135,out value); // key1063, Morton836240
                case 161:return PortalLandingLayout020.TryOriginalObstacle020(136,out value); // key1062, Morton840832
                default:value=default;return false;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world metres/Y-up and unchanged provider poses; only semantic body302 is removed for the walkable tavern.
// Array Formats:162 finite Morton/Key ordered records:136 preserved originals,18 tavern kind10/key8000..8017 and8 garden kind8/key6000..6007. Direct scalar dispatch has no runtime search/sort/allocation.
// Dependencies/Pointers: Generated from exact provider records by semantic identity, not inherited outer slots. Root must regenerate from frozen provider records when adding street kind9/key7000..7007;170 obstacles then share the same Stand/Move/native collider stream.
