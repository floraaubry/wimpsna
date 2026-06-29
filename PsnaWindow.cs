using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules.Managers;
using TokenPermission = Gw2Sharp.WebApi.V2.Models.TokenPermission;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WhereIsMyPSNA
{
    public class PsnaWindow : StandardWindow
    {
        private static readonly Logger     Logger = Logger.GetLogger<PsnaWindow>();
        private static readonly HttpClient Http   = new HttpClient();

        private const string SheetCsvUrl      = "https://docs.google.com/spreadsheets/d/14Jf0-RAcva1w-vx71vK1YvRIcGi4sQfWcrZHyhwAzwQ/export?format=csv&gid=0";
        private const string Gw2ItemsUrl      = "https://api.guildwars2.com/v2/items?ids=";
        private const string Gw2KarmaCurrency = "https://api.guildwars2.com/v2/currencies/2";

        private const int RowHeight     = 102;
        private const int RowSpacing    = 0;
        private const int RowPadding    = 20;
        private const int IconSize      = 42;
        private const int KarmaIconSize = 24;
        private const int TitleOffset   = 26;

        private static readonly int[] ScheduleToSheetCol = { 0, 1, 3, 2, 4, 5 };

        private readonly Image[]          _recipeIcons  = new Image[6];
        private readonly Label[]          _recipeLabels = new Label[6];
        private readonly Image[]          _karmaIcons   = new Image[6];
        private readonly Label[]          _karmaLabels  = new Label[6];
        private readonly Label[]          _knownLabels  = new Label[6];
        private readonly LoadingSpinner[] _spinners     = new LoadingSpinner[6];

        private readonly RecipeTooltip   _tooltip;
        private readonly Gw2ApiManager   _apiManager;
        private readonly int[]           _slotItemIds             = new int[6];
        private readonly Texture2D[]     _slotCraftedIconTextures = new Texture2D[6];

        private Texture2D               _karmaTexture;
        private bool                    _coinTexturesLoaded;
        private int                     _hoveredSlot = -1;
        private CancellationTokenSource _spinnerCts;
        private volatile HashSet<int>   _knownCraftingRecipeIds;

        private const string CoinGoldUrl   = "https://render.guildwars2.com/file/090A980A96D39FD36FBB004903644C6DBEFB1FFB/156904.png";
        private const string CoinSilverUrl = "https://render.guildwars2.com/file/E5A2197D78ECE4AE0349C8B3710D033D22DB0DA6/156907.png";
        private const string CoinCopperUrl = "https://render.guildwars2.com/file/6CF8F96A3299CFC75D5CC90617C3C70331A1EF0E/156902.png";

        public PsnaWindow(ContentsManager contentsManager, Gw2ApiManager apiManager)
            : base(
                contentsManager.GetTexture("window_bg.png"),
                new Rectangle(40, 26, 913, 691),
                new Rectangle(70, 40, 839, 636))
        {
            Parent        = GameService.Graphics.SpriteScreen;
            Title         = "Where Is My PSNA";
            Subtitle      = "Daily Locations";
            Emblem        = contentsManager.GetTexture("window_emblem.png");
            SavesPosition = true;
            Id            = "PsnaWindow_com.odizinne.whereismypsna_38d37290-b5f9-447d-97ea-45b0b50e5f56";

            _apiManager = apiManager;
            _tooltip    = new RecipeTooltip();

            BuildRows(PsnaSchedule.GetTodaysLocations());
        }

        private void BuildRows(PsnaSchedule.AgentLocation[] locations)
        {
            int y        = 0;
            int rowWidth = ContentRegion.Width;

            for (int i = 0; i < locations.Length; i++)
            {
                var loc = locations[i];

                var row = new Panel
                {
                    Parent     = this,
                    Size       = new Point(rowWidth, RowHeight),
                    Location   = new Point(0, y),
                    ShowBorder = true,
                    Title      = loc.Npc,
                };

                int leftTextX = IconSize + 18;
                int rightColW = 200;
                int rightColX = rowWidth - rightColW - 12;
                int leftTextW = rightColX - leftTextX - 8;

                int capturedY = y;
                var copyBtn = new StandardButton
                {
                    Parent   = this,
                    Text     = "Copy",
                    Size     = new Point(70, TitleOffset),
                    Location = new Point(rowWidth - 70 - 10, capturedY + 5),
                };

                int capturedIdx = i;

                _recipeIcons[i] = new Image
                {
                    Parent   = row,
                    Size     = new Point(IconSize, IconSize),
                    Location = new Point(8, 10),
                    Visible  = false,
                };


                _spinners[i] = new LoadingSpinner
                {
                    Parent   = row,
                    Size     = new Point(20, 20),
                    Location = new Point(leftTextX, 10),
                    Visible  = true,
                };

                _recipeLabels[i] = new Label
                {
                    Parent   = row,
                    Text     = "",
                    Location = new Point(leftTextX, 12),
                    Size     = new Point(leftTextW, 18),
                    Font     = GameService.Content.DefaultFont14,
                    Visible  = false,
                };

                _karmaIcons[i] = new Image
                {
                    Parent   = row,
                    Size     = new Point(KarmaIconSize, KarmaIconSize),
                    Location = new Point(leftTextX, 32),
                    Visible  = false,
                };

                _karmaLabels[i] = new Label
                {
                    Parent   = row,
                    Text     = "25,200",
                    Location = new Point(leftTextX + KarmaIconSize + 4, 38),
                    Size     = new Point(100, 16),
                    Font     = GameService.Content.DefaultFont14,
                    Visible  = false,
                };

                _knownLabels[i] = new Label
                {
                    Parent              = this,
                    Text                = "You already know this recipe",
                    Location            = new Point(rowWidth - 70 - 10 - 8 - 240, capturedY + 5),
                    Size                = new Point(240, TitleOffset),
                    Font                = GameService.Content.DefaultFont14,
                    TextColor           = new Color(220, 80, 80),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment   = VerticalAlignment.Middle,
                    Visible             = false,
                };

                new Label
                {
                    Parent              = row,
                    Text                = loc.Location,
                    Location            = new Point(rightColX, 10),
                    Size                = new Point(rightColW, 18),
                    Font                = GameService.Content.DefaultFont14,
                    WrapText            = false,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };

                new Label
                {
                    Parent              = row,
                    Text                = loc.Map,
                    Location            = new Point(rightColX, 32),
                    Size                = new Point(rightColW, 16),
                    Font                = GameService.Content.DefaultFont12,
                    TextColor           = Color.LightGray,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };

                var capturedLoc = loc;
                copyBtn.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(capturedLoc.ChatCode))
                    {
                        ScreenNotification.ShowNotification("No chat code for this location yet");
                        return;
                    }
                    TrySetClipboardText(capturedLoc.ChatCode);
                };

                y += RowHeight + RowSpacing;
            }
        }

        public new void ToggleWindow()
        {
            base.ToggleWindow();

            if (Visible)
            {
                _ = FetchAccountRecipesAsync();
                ResetToLoading();
                _spinnerCts?.Cancel();
                _spinnerCts = new CancellationTokenSource();
                Task.Run(() => FetchAllAsync(_spinnerCts.Token));
            }
            else
            {
                _tooltip.Visible = false;
            }
        }

        private async Task FetchAccountRecipesAsync()
        {
            if (!_apiManager.HasPermissions(new[] { TokenPermission.Account }))
            {
                Logger.Warn("Skipping account recipes fetch: missing Account permission.");
                return;
            }
            try
            {
                var recipes = await _apiManager.Gw2ApiClient.V2.Account.Recipes.GetAsync();
                _knownCraftingRecipeIds = new HashSet<int>(recipes);

                GameService.Graphics.QueueMainThreadRender(_ =>
                {
                    for (int i = 0; i < 6; i++)
                    {
                        int slotItemId = _slotItemIds[i];
                        if (slotItemId <= 0 || !RecipeDefs.ByRecipeSheetId.TryGetValue(slotItemId, out var def))
                            continue;
                        bool isKnown = def.CraftingRecipeIds != null
                                       && Array.Exists(def.CraftingRecipeIds, id => _knownCraftingRecipeIds.Contains(id));
                        _knownLabels[i].Visible = isKnown;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to fetch account recipes.");
            }
        }

        private void ResetToLoading()
        {
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                GameService.Graphics.QueueMainThreadRender(_ =>
                {
                    _recipeIcons[idx].Visible    = false;
                    _recipeLabels[idx].Visible   = false;
                    _recipeLabels[idx].Text      = "";
                    _spinners[idx].Visible       = true;
                    _karmaIcons[idx].Visible     = false;
                    _karmaLabels[idx].Visible    = false;
                    _knownLabels[idx].Visible    = false;
                });
            }
        }

        private async Task FetchAllAsync(CancellationToken ct)
        {
            try
            {
                if (!_coinTexturesLoaded)
                {
                    var coins = await Task.WhenAll(
                        FetchTextureAsync(CoinGoldUrl),
                        FetchTextureAsync(CoinSilverUrl),
                        FetchTextureAsync(CoinCopperUrl));
                    _coinTexturesLoaded = true;
                    GameService.Graphics.QueueMainThreadRender(_ =>
                        _tooltip.SetCoinTextures(coins[0], coins[1], coins[2]));
                }

                var karmaTask = FetchKarmaIconAsync();
                var sheetTask = FetchSheetAsync();
                await Task.WhenAll(karmaTask, sheetTask);

                if (ct.IsCancellationRequested) return;

                _karmaTexture = karmaTask.Result;

                if (_karmaTexture != null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        int idx = i;
                        GameService.Graphics.QueueMainThreadRender(_ =>
                            _karmaIcons[idx].Texture = _karmaTexture);
                    }
                }

                var (itemIds, todayFlags) = sheetTask.Result;

                var validIds = itemIds
                    .Select((id, col) => new { id, col })
                    .Where(x => x.id > 0 && todayFlags[x.col])
                    .Select(x => x.id)
                    .Distinct()
                    .ToArray();

                var items = validIds.Length > 0
                    ? await FetchItemsAsync(validIds)
                    : new Dictionary<int, Gw2Item>();

                var craftedIdPerSlot = new int[6];
                for (int i = 0; i < 6; i++)
                {
                    int sheetCol = ScheduleToSheetCol[i];
                    if (todayFlags[sheetCol] && itemIds[sheetCol] > 0
                        && RecipeDefs.ByRecipeSheetId.TryGetValue(itemIds[sheetCol], out var def)
                        && def.ItemIds.Length > 0)
                        craftedIdPerSlot[i] = def.ItemIds[0];
                }

                var distinctCraftedIds = craftedIdPerSlot.Where(id => id > 0).Distinct().ToArray();
                var craftedItems = distinctCraftedIds.Length > 0
                    ? await FetchItemsAsync(distinctCraftedIds)
                    : new Dictionary<int, Gw2Item>();

                for (int i = 0; i < 6; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    int idx      = i;
                    int sheetCol = ScheduleToSheetCol[i];

                    if (!todayFlags[sheetCol] || itemIds[sheetCol] <= 0 || !items.TryGetValue(itemIds[sheetCol], out var item))
                    {
                        GameService.Graphics.QueueMainThreadRender(_ =>
                        {
                            _spinners[idx].Visible     = false;
                            _recipeLabels[idx].Text    = "Not determined yet";
                            _recipeLabels[idx].Visible = true;
                        });
                        continue;
                    }

                    _slotItemIds[idx] = itemIds[sheetCol];

                    if (craftedIdPerSlot[idx] > 0
                        && craftedItems.TryGetValue(craftedIdPerSlot[idx], out var craftedItem)
                        && !string.IsNullOrEmpty(craftedItem.Icon))
                    {
                        var craftedTex = await FetchTextureAsync(craftedItem.Icon);
                        if (craftedTex != null)
                            _slotCraftedIconTextures[idx] = craftedTex;
                    }

                    Texture2D iconTex = null;
                    if (!string.IsNullOrEmpty(item.Icon))
                        iconTex = await FetchTextureAsync(item.Icon);

                    if (ct.IsCancellationRequested) return;

                    var capturedItem = item;
                    var capturedIcon = iconTex;

                    bool isKnown = false;
                    if (RecipeDefs.ByRecipeSheetId.TryGetValue(itemIds[sheetCol], out var knownDef))
                    {
                        var known = _knownCraftingRecipeIds;
                        isKnown = known != null && knownDef.CraftingRecipeIds != null
                                  && Array.Exists(knownDef.CraftingRecipeIds, id => known.Contains(id));
                    }

                    bool capturedKnown = isKnown;
                    GameService.Graphics.QueueMainThreadRender(_ =>
                    {
                        _spinners[idx].Visible = false;

                        _recipeLabels[idx].Text    = capturedItem.Name;
                        _recipeLabels[idx].Visible = true;

                        if (capturedIcon != null)
                        {
                            _recipeIcons[idx].Texture = capturedIcon;
                            _recipeIcons[idx].Visible = true;
                        }

                        _karmaLabels[idx].Visible = true;
                        _karmaIcons[idx].Visible  = _karmaTexture != null;
                        _knownLabels[idx].Visible = capturedKnown;
                    });
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Logger.Warn(ex, "Failed to fetch PSNA data.");
                for (int i = 0; i < 6; i++)
                {
                    int idx = i;
                    GameService.Graphics.QueueMainThreadRender(_ =>
                    {
                        _spinners[idx].Visible     = false;
                        _recipeLabels[idx].Text    = "Fetch failed";
                        _recipeLabels[idx].Visible = true;
                    });
                }
            }
        }

        private async Task<(int[] itemIds, bool[] todayFlags)> FetchSheetAsync()
        {
            var csv   = await Http.GetStringAsync(SheetCsvUrl);
            var lines = csv.Split('\n');

            var idLine   = lines[1].Split(',');
            var dateLine = lines[2].Split(',');

            string currentDate = dateLine.Length > 6 ? dateLine[6].Trim().Trim('"') : "";

            var itemIds    = new int[6];
            var todayFlags = new bool[6];

            for (int col = 0; col < 6; col++)
            {
                if (col < idLine.Length && int.TryParse(idLine[col].Trim().Trim('"'), out int id))
                    itemIds[col] = id;

                string colDate = col < dateLine.Length ? dateLine[col].Trim().Trim('"') : "";
                todayFlags[col] = !string.IsNullOrEmpty(currentDate) && colDate == currentDate;
            }

            return (itemIds, todayFlags);
        }

        private async Task<Dictionary<int, Gw2Item>> FetchItemsAsync(int[] ids)
        {
            var json  = await Http.GetStringAsync(Gw2ItemsUrl + string.Join(",", ids));
            var items = JsonConvert.DeserializeObject<List<Gw2Item>>(json);
            return items.ToDictionary(x => x.Id);
        }

        private async Task<Texture2D> FetchKarmaIconAsync()
        {
            try
            {
                var json     = await Http.GetStringAsync(Gw2KarmaCurrency);
                var currency = JsonConvert.DeserializeObject<Gw2Currency>(json);
                if (!string.IsNullOrEmpty(currency?.Icon))
                    return await FetchTextureAsync(currency.Icon);
            }
            catch (Exception ex) { Logger.Warn(ex, "Failed to fetch karma icon."); }
            return null;
        }

        private async Task<Texture2D> FetchTextureAsync(string url)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                var tcs   = new TaskCompletionSource<Texture2D>();

                GameService.Graphics.QueueMainThreadRender(gd =>
                {
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        tcs.SetResult(Texture2D.FromStream(gd, ms));
                    }
                    catch (Exception ex) { tcs.SetException(ex); }
                });

                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to fetch texture: {url}");
                return null;
            }
        }

        private static void TrySetClipboardText(string text)
        {
            try { System.Windows.Forms.Clipboard.SetText(text); }
            catch (Exception ex) { Logger.Warn(ex, "Failed to set clipboard."); }
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (!Visible) return;

            var mouse = GameService.Input.Mouse.Position;
            int hitSlot = -1;

            for (int i = 0; i < 6; i++)
            {
                if (_recipeIcons[i].Visible && _recipeIcons[i].AbsoluteBounds.Contains(mouse.X, mouse.Y))
                {
                    hitSlot = i;
                    break;
                }
            }

            if (hitSlot >= 0)
            {
                _tooltip.MoveTo(mouse.X, mouse.Y);

                if (hitSlot != _hoveredSlot)
                {
                    _hoveredSlot = hitSlot;
                    int itemId = _slotItemIds[hitSlot];
                    if (itemId > 0 && RecipeDefs.ByRecipeSheetId.TryGetValue(itemId, out var def))
                    {
                        _tooltip.SetRecipe(def, _slotCraftedIconTextures[hitSlot]);
                        _tooltip.Visible = true;
                    }
                    else
                    {
                        _tooltip.Visible = false;
                    }
                }
            }
            else
            {
                _hoveredSlot    = -1;
                _tooltip.Visible = false;
            }
        }

        protected override void DisposeControl()
        {
            _spinnerCts?.Cancel();
            _spinnerCts?.Dispose();
            _tooltip?.Dispose();
            base.DisposeControl();
        }

        private class Gw2Item
        {
            [JsonProperty("id")]   public int    Id   { get; set; }
            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("icon")] public string Icon { get; set; }
        }

        private class Gw2Currency
        {
            [JsonProperty("icon")] public string Icon { get; set; }
        }
    }
}
