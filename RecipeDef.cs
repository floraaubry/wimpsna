namespace WhereIsMyPSNA
{
    internal class RecipeDef
    {
        public int[]  ItemIds           { get; set; }
        public int[]  RecipeSheetIds    { get; set; }
        public int[]  CraftingRecipeIds { get; set; }
        public string Name         { get; set; }
        public string Type         { get; set; }
        public string DetailType   { get; set; }
        public string Rarity       { get; set; }
        public int    Level        { get; set; }
        public string Description  { get; set; }
        public int    DurationSecs { get; set; }
        public string Binding      { get; set; }
        public int    VendorValue  { get; set; }
    }
}
