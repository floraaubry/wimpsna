using System;

namespace WhereIsMyPSNA
{
    public static class PsnaSchedule
    {
        public class AgentLocation
        {
            public string Npc      { get; set; }
            public string Map      { get; set; }
            public string Location { get; set; }
            public string ChatCode { get; set; }

            public AgentLocation(string npc, string map, string location, string chatCode = null)
            {
                Npc      = npc;
                Map      = map;
                Location = location;
                ChatCode = chatCode;
            }
        }

        private static readonly DateTime Anchor = new DateTime(2026, 5, 7, 8, 0, 0, DateTimeKind.Utc);

        private static readonly AgentLocation[][] Cycle =
        {
            // Day 0 = Thursday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "The Silverwastes",     "Blue Oasis",                     "[&BKsHAAA=]"),
                new AgentLocation("The Fox",              "Brisban Wildlands",    "Seraph Protectors",              "[&BF0AAAA=]"),
                new AgentLocation("Specialist Yana",      "Straits of Devastation","Armada Harbor",                "[&BO4CAAA=]"),
                new AgentLocation("Lady Derwena",         "Queensdale",           "Altar Brook Trading Post",       "[&BIMAAAA=]"),
                new AgentLocation("Despina Katelyn",      "Lornar's Pass",        "Rocklair",                       "[&BF0GAAA=]"),
                new AgentLocation("Verma Giftrender",     "Iron Marches",         "Village of Scalecatch Waypoint", "[&BOcBAAA=]"),
            },
            // Day 1 = Friday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "Dry Top",              "Repair Station",                 "[&BJQHAAA=]"),
                new AgentLocation("The Fox",              "Mount Maelstrom",      "Breth Ayahusasca",               "[&BMwCAAA=]"),
                new AgentLocation("Specialist Yana",      "Malchor's Leap",       "Shelter Docks",                  "[&BJsCAAA=]"),
                new AgentLocation("Lady Derwena",         "Southsun Cove",        "Pearl Islet Waypoint",           "[&BNUGAAA=]"),
                new AgentLocation("Despina Katelyn",      "Wayfarer Foothills",   "Dolyak Pass Waypoint",           "[&BHsBAAA=]"),
                new AgentLocation("Verma Giftrender",     "Fields of Ruin",       "Hawkgates Waypoint",             "[&BNMAAAA=]"),
            },
            // Day 2 = Saturday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "The Silverwastes",     "Camp Resolve Waypoint",          "[&BH8HAAA=]"),
                new AgentLocation("The Fox",              "Mount Maelstrom",      "Gallant's Folly",                "[&BLkCAAA=]"),
                new AgentLocation("Specialist Yana",      "Cursed Shore",         "Augur's Torch",                  "[&BBEDAAA=]"),
                new AgentLocation("Lady Derwena",         "Gendarran Fields",     "Vigil Keep Waypoint",            "[&BJIBAAA=]"),
                new AgentLocation("Despina Katelyn",      "Timberline Falls",     "Balddistead",                    "[&BEICAAA=]"),
                new AgentLocation("Verma Giftrender",     "Diessa Plateau",       "Bovarin Estate",                 "[&BBABAAA=]"),
            },
            // Day 3 = Sunday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "Dry Top",              "Azarr's Arbor",                  "[&BIkHAAA=]"),
                new AgentLocation("The Fox",              "Caledon Forest",       "Mabon Waypoint",                 "[&BDoBAAA=]"),
                new AgentLocation("Specialist Yana",      "Straits of Devastation","Fort Trinity Waypoint",         "[&BO4CAAA=]"),
                new AgentLocation("Lady Derwena",         "Bloodtide Coast",      "Mudflat Camp",                   "[&BC0AAAA=]"),
                new AgentLocation("Despina Katelyn",      "Frostgorge Sound",     "Blue Ice Shining Waypoint",      "[&BIUCAAA=]"),
                new AgentLocation("Verma Giftrender",     "Fireheart Rise",       "Snow Ridge Camp Waypoint",       "[&BCECAAA=]"),
            },
            // Day 4 = Monday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "Dry Top",              "Restoration Refuge",             "[&BIcHAAA=]"),
                new AgentLocation("The Fox",              "Caledon Forest",       "Lionguard Waystation Waypoint",  "[&BEwDAAA=]"),
                new AgentLocation("Specialist Yana",      "Straits of Devastation","Rally Waypoint",               "[&BNIEAAA=]"),
                new AgentLocation("Lady Derwena",         "Bloodtide Coast",      "Marshwatch Haven Waypoint",      "[&BKYBAAA=]"),
                new AgentLocation("Despina Katelyn",      "Frostgorge Sound",     "Ridgerock Camp Waypoint",        "[&BIMCAAA=]"),
                new AgentLocation("Verma Giftrender",     "Fireheart Rise",       "Haymal Gore",                    "[&BA8CAAA=]"),
            },
            // Day 5 = Tuesday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "The Silverwastes",     "Camp Resolve Waypoint",          "[&BH8HAAA=]"),
                new AgentLocation("The Fox",              "Metrica Province",     "Desider Atum Waypoint",          "[&BEgAAAA=]"),
                new AgentLocation("Specialist Yana",      "Malchor's Leap",       "Waste Hollows Waypoint",         "[&BKgCAAA=]"),
                new AgentLocation("Lady Derwena",         "Kessex Hills",         "Garenhoff",                      "[&BBkAAAA=]"),
                new AgentLocation("Despina Katelyn",      "Dredgehaunt Cliffs",   "Travelen's Waypoint",            "[&BGQCAAA=]"),
                new AgentLocation("Verma Giftrender",     "Plains of Ashford",    "Temperus Point Waypoint",        "[&BIMBAAA=]"),
            },
            // Day 6 = Wednesday
            new[]
            {
                new AgentLocation("Mehem the Traveled",  "Dry Top",              "Town of Prosperity",             "[&BH4HAAA=]"),
                new AgentLocation("The Fox",              "Sparkfly Fen",         "Swampwatch Post",                "[&BMIBAAA=]"),
                new AgentLocation("Specialist Yana",      "Cursed Shore",         "Caer Shadowfain",                "[&BP0CAAA=]"),
                new AgentLocation("Lady Derwena",         "Harathi Hinterlands",  "Shieldbluff Waypoint",           "[&BKYAAAA=]"),
                new AgentLocation("Despina Katelyn",      "Snowden Drifts",       "Mennerheim",                     "[&BDgDAAA=]"),
                new AgentLocation("Verma Giftrender",     "Blazeridge Steppes",   "Ferrusatos Village",             "[&BPEBAAA=]"),
            },
        };

        public static AgentLocation[] GetTodaysLocations(DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            var daysSinceAnchor = (int)Math.Floor((now - Anchor).TotalDays);
            var index = ((daysSinceAnchor % 7) + 7) % 7;
            return Cycle[index];
        }
    }
}