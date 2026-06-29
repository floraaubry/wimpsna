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

        private readonly Panel[]          _rowPanels    = new Panel[6];
        private readonly Image[]          _recipeIcons  = new Image[6];
        private readonly Label[]          _recipeLabels = new Label[6];
        private readonly Image[]          _karmaIcons   = new Image[6];
        private readonly Label[]          _karmaLabels  = new Label[6];
        private readonly Label[]          _knownLabels  = new Label[6];
        private readonly StandardButton[] _copyButtons  = new StandardButton[6];
        private LoadingSpinner            _centerSpinner;
        private Label                     _statusLabel;

        private readonly RecipeTooltip   _tooltip;
        private readonly Gw2ApiManager   _apiManager;
        private readonly int[]           _slotItemIds             = new int[6];
        private readonly Texture2D[]     _slotCraftedIconTextures = new Texture2D[6];

        private bool                    _coinTexturesLoaded;
        private int                     _hoveredSlot = -1;
        private CancellationTokenSource _spinnerCts;
        private volatile HashSet<int>   _knownCraftingRecipeIds;

        private float _fadeOpacity;
        private bool  _fadeActive;
        private const float FadeDuration = 0.35f;

        private const string CoinGoldUrl   = "https://render.guildwars2.com/file/090A980A96D39FD36FBB004903644C6DBEFB1FFB/156904.png";
        private const string CoinSilverUrl = "https://render.guildwars2.com/file/E5A2197D78ECE4AE0349C8B3710D033D22DB0DA6/156907.png";
        private const string CoinCopperUrl = "https://render.guildwars2.com/file/6CF8F96A3299CFC75D5CC90617C3C70331A1EF0E/156902.png";

        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        private class SlotData
        {
            public bool      IsNotDetermined;
            public int       ItemId;
            public string    ItemName;
            public Texture2D IconTexture;
            public Texture2D CraftedIconTexture;
        }

        private class FetchCache
        {
            public DateTime   Timestamp;
            public SlotData[] Slots;
            public Texture2D  KarmaTexture;
        }

        private FetchCache _cache;

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

            const int spinnerSize = 48;
            int spinnerY = 6 * RowHeight / 2 - spinnerSize / 2;
            _centerSpinner = new LoadingSpinner
            {
                Parent   = this,
                Size     = new Point(spinnerSize, spinnerSize),
                Location = new Point(ContentRegion.Width / 2 - spinnerSize / 2, spinnerY),
                Visible  = false,
            };

            _statusLabel = new Label
            {
                Parent              = this,
                Text                = "",
                Location            = new Point(0, spinnerY + spinnerSize + 8),
                Size                = new Point(ContentRegion.Width, 20),
                Font                = GameService.Content.DefaultFont14,
                TextColor           = Color.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible             = false,
            };
        }

        private void BuildRows(PsnaSchedule.AgentLocation[] locations)
        {
            int y        = 0;
            int rowWidth = ContentRegion.Width;

            for (int i = 0; i < locations.Length; i++)
            {
                var loc = locations[i];

                _rowPanels[i] = new Panel
                {
                    Parent     = this,
                    Size       = new Point(rowWidth, RowHeight),
                    Location   = new Point(0, y),
                    ShowBorder = true,
                    Title      = loc.Npc,
                };
                var row = _rowPanels[i];

                int leftTextX = IconSize + 18;
                int rightColW = 200;
                int rightColX = rowWidth - rightColW - 12;
                int leftTextW = rightColX - leftTextX - 8;

                int capturedY = y;
                _copyButtons[i] = new StandardButton
                {
                    Parent   = this,
                    Text     = "Copy",
                    Size     = new Point(70, TitleOffset),
                    Location = new Point(rowWidth - 70 - 10, capturedY + 5),
                    Visible  = false,
                };

                _recipeIcons[i] = new Image
                {
                    Parent   = row,
                    Size     = new Point(IconSize, IconSize),
                    Location = new Point(8, 10),
                    Visible  = false,
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

                var capturedLoc = loc;
                _copyButtons[i].Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(capturedLoc.ChatCode))
                    {
                        ScreenNotification.ShowNotification("No chat code for this location yet");
                        return;
                    }
                    TrySetClipboardText(capturedLoc.ChatCode);
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

                y += RowHeight + RowSpacing;
            }
        }

        public new void ToggleWindow()
        {
            base.ToggleWindow();

            if (Visible)
            {
                if (_cache != null && (DateTime.UtcNow - _cache.Timestamp) < CacheExpiry)
                {
                    ApplyCache(_cache);
                    _ = RefreshKnownLabelsAsync();
                    return;
                }

                ShowLoadingSpinner();
                _spinnerCts?.Cancel();
                _spinnerCts = new CancellationTokenSource();
                var accountTask = FetchAccountRecipesAsync();
                Task.Run(() => FetchAllAsync(_spinnerCts.Token, accountTask));
            }
            else
            {
                _tooltip.Visible = false;
            }
        }

        private void ShowLoadingSpinner()
        {
            GameService.Graphics.QueueMainThreadRender(_ =>
            {
                _fadeActive            = false;
                _centerSpinner.Visible = true;
                _statusLabel.Visible   = true;
                for (int i = 0; i < 6; i++)
                {
                    _rowPanels[i].Visible    = false;
                    _recipeIcons[i].Visible  = false;
                    _recipeLabels[i].Visible = false;
                    _karmaIcons[i].Visible   = false;
                    _karmaLabels[i].Visible  = false;
                    _knownLabels[i].Visible  = false;
                    _copyButtons[i].Visible  = false;
                }
            });
        }

        private void SetUiOpacity(float opacity)
        {
            for (int i = 0; i < 6; i++)
            {
                _rowPanels[i].Opacity    = opacity;
                _recipeIcons[i].Opacity  = opacity;
                _recipeLabels[i].Opacity = opacity;
                _karmaIcons[i].Opacity   = opacity;
                _karmaLabels[i].Opacity  = opacity;
                _knownLabels[i].Opacity  = opacity;
                _copyButtons[i].Opacity  = opacity;
            }
        }

        private void StartFadeIn()
        {
            _fadeOpacity = 0f;
            _fadeActive  = true;
            SetUiOpacity(0f);
        }

        private void SetStatus(string text)
        {
            GameService.Graphics.QueueMainThreadRender(_ =>
            {
                _statusLabel.Text = text;
            });
        }

        private void ApplyCache(FetchCache cache)
        {
            GameService.Graphics.QueueMainThreadRender(_ =>
            {
                _centerSpinner.Visible = false;
                _statusLabel.Visible   = false;
                for (int i = 0; i < 6; i++)
                {
                    var slot = cache.Slots[i];
                    _rowPanels[i].Visible   = true;
                    _copyButtons[i].Visible = true;

                    if (slot.IsNotDetermined)
                    {
                        _recipeLabels[i].Text    = "Not determined yet";
                        _recipeLabels[i].Visible = true;
                        _recipeIcons[i].Visible  = false;
                        _karmaIcons[i].Visible   = false;
                        _karmaLabels[i].Visible  = false;
                        _knownLabels[i].Visible  = false;
                    }
                    else
                    {
                        _slotItemIds[i]             = slot.ItemId;
                        _slotCraftedIconTextures[i] = slot.CraftedIconTexture;

                        _recipeLabels[i].Text    = slot.ItemName;
                        _recipeLabels[i].Visible = true;
                        _recipeIcons[i].Texture  = slot.IconTexture;
                        _recipeIcons[i].Visible  = slot.IconTexture != null;
                        _karmaIcons[i].Texture   = cache.KarmaTexture;
                        _karmaIcons[i].Visible   = cache.KarmaTexture != null;
                        _karmaLabels[i].Visible  = true;
                        _knownLabels[i].Visible  = false;
                    }
                }
                StartFadeIn();
            });
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
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to fetch account recipes.");
            }
        }

        private async Task RefreshKnownLabelsAsync()
        {
            await FetchAccountRecipesAsync();
            var known = _knownCraftingRecipeIds;
            GameService.Graphics.QueueMainThreadRender(_ =>
            {
                for (int i = 0; i < 6; i++)
                {
                    int itemId = _slotItemIds[i];
                    if (itemId <= 0 || !RecipeDefs.ByRecipeSheetId.TryGetValue(itemId, out var def))
                    {
                        _knownLabels[i].Visible = false;
                        continue;
                    }
                    bool isKnown = known != null && def.CraftingRecipeIds != null
                                   && Array.Exists(def.CraftingRecipeIds, id => known.Contains(id));
                    _knownLabels[i].Visible = isKnown;
                }
            });
        }

        private async Task FetchAllAsync(CancellationToken ct, Task accountRecipesTask)
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

                SetStatus("Fetching schedule...");
                var karmaTask = FetchKarmaIconAsync();
                var sheetTask = FetchSheetAsync();
                await Task.WhenAll(karmaTask, sheetTask);

                if (ct.IsCancellationRequested) return;

                var karmaTexture          = karmaTask.Result;
                var (itemIds, todayFlags) = sheetTask.Result;

                var validIds = itemIds
                    .Select((id, col) => new { id, col })
                    .Where(x => x.id > 0 && todayFlags[x.col])
                    .Select(x => x.id)
                    .Distinct()
                    .ToArray();

                SetStatus("Fetching recipe data...");
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

                var slotResults      = new SlotData[6];
                var iconTasks        = new Task<Texture2D>[6];
                var craftedIconTasks = new Task<Texture2D>[6];

                for (int i = 0; i < 6; i++)
                {
                    int sheetCol   = ScheduleToSheetCol[i];
                    slotResults[i] = new SlotData();

                    if (!todayFlags[sheetCol] || itemIds[sheetCol] <= 0
                        || !items.TryGetValue(itemIds[sheetCol], out var item))
                    {
                        slotResults[i].IsNotDetermined = true;
                        iconTasks[i]        = Task.FromResult<Texture2D>(null);
                        craftedIconTasks[i] = Task.FromResult<Texture2D>(null);
                        continue;
                    }

                    slotResults[i].ItemId   = itemIds[sheetCol];
                    slotResults[i].ItemName = item.Name;

                    iconTasks[i] = string.IsNullOrEmpty(item.Icon)
                        ? Task.FromResult<Texture2D>(null)
                        : FetchTextureAsync(item.Icon);

                    craftedIconTasks[i] = craftedIdPerSlot[i] > 0
                        && craftedItems.TryGetValue(craftedIdPerSlot[i], out var craftedItem)
                        && !string.IsNullOrEmpty(craftedItem.Icon)
                            ? FetchTextureAsync(craftedItem.Icon)
                            : Task.FromResult<Texture2D>(null);
                }

                SetStatus("Fetching icons...");
                await Task.WhenAll(iconTasks.Concat(craftedIconTasks).ToArray());

                if (ct.IsCancellationRequested) return;

                for (int i = 0; i < 6; i++)
                {
                    if (!slotResults[i].IsNotDetermined)
                    {
                        slotResults[i].IconTexture        = iconTasks[i].Result;
                        slotResults[i].CraftedIconTexture = craftedIconTasks[i].Result;
                    }
                }

                SetStatus("Checking known recipes...");
                await accountRecipesTask;

                if (ct.IsCancellationRequested) return;

                var known = _knownCraftingRecipeIds;

                if (slotResults.Any(s => !s.IsNotDetermined))
                {
                    _cache = new FetchCache
                    {
                        Timestamp    = DateTime.UtcNow,
                        Slots        = slotResults,
                        KarmaTexture = karmaTexture,
                    };
                }

                GameService.Graphics.QueueMainThreadRender(_ =>
                {
                    _centerSpinner.Visible = false;
                _statusLabel.Visible   = false;

                    for (int i = 0; i < 6; i++)
                    {
                        var slot = slotResults[i];
                        _rowPanels[i].Visible   = true;
                        _copyButtons[i].Visible = true;

                        if (slot.IsNotDetermined)
                        {
                            _recipeLabels[i].Text    = "Not determined yet";
                            _recipeLabels[i].Visible = true;
                            continue;
                        }

                        _slotItemIds[i]             = slot.ItemId;
                        _slotCraftedIconTextures[i] = slot.CraftedIconTexture;

                        _recipeLabels[i].Text    = slot.ItemName;
                        _recipeLabels[i].Visible = true;

                        if (slot.IconTexture != null)
                        {
                            _recipeIcons[i].Texture = slot.IconTexture;
                            _recipeIcons[i].Visible = true;
                        }

                        _karmaIcons[i].Texture  = karmaTexture;
                        _karmaLabels[i].Visible = true;
                        _karmaIcons[i].Visible  = karmaTexture != null;

                        if (RecipeDefs.ByRecipeSheetId.TryGetValue(slot.ItemId, out var knownDef))
                        {
                            bool isKnown = known != null && knownDef.CraftingRecipeIds != null
                                           && Array.Exists(knownDef.CraftingRecipeIds, id => known.Contains(id));
                            _knownLabels[i].Visible = isKnown;
                        }
                    }
                    StartFadeIn();
                });
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Logger.Warn(ex, "Failed to fetch PSNA data.");
                GameService.Graphics.QueueMainThreadRender(_ =>
                {
                    _centerSpinner.Visible = false;
                _statusLabel.Visible   = false;
                    for (int i = 0; i < 6; i++)
                    {
                        _rowPanels[i].Visible    = true;
                        _recipeLabels[i].Text    = "Fetch failed";
                        _recipeLabels[i].Visible = true;
                        _copyButtons[i].Visible  = true;
                    }
                    StartFadeIn();
                });
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

            if (_fadeActive)
            {
                _fadeOpacity += (float)(gameTime.ElapsedGameTime.TotalSeconds / FadeDuration);
                if (_fadeOpacity >= 1f)
                {
                    _fadeOpacity = 1f;
                    _fadeActive  = false;
                }
                SetUiOpacity(_fadeOpacity);
            }

            var mouse   = GameService.Input.Mouse.Position;
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
                    int itemId   = _slotItemIds[hitSlot];
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
                _hoveredSlot     = -1;
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
