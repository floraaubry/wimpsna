using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace WhereIsMyPSNA
{
    [Export(typeof(Module))]
    public class WhereIsMyPsnaModule : Module
    {
        private ModuleParameters _moduleParameters;

        private CornerIcon         _cornerIcon;
        private PsnaWindow         _psnaWindow;
        private SettingEntry<bool> _hideKnownNpcs;

        [ImportingConstructor]
        public WhereIsMyPsnaModule([Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters)
        {
            _moduleParameters = moduleParameters;
        }

        protected override void DefineSettings(SettingCollection settings)
        {
            _hideKnownNpcs = settings.DefineSetting(
                "HideKnownNpcs",
                false,
                () => "Hide NPC if recipe is already known",
                () => "Hides the panel and copy button for NPCs whose today's recipe you already know."
            );
        }

        protected override void Initialize() { }

        protected override async Task LoadAsync() { }

        protected override void OnModuleLoaded(EventArgs e)
        {
            var icon = _moduleParameters.ContentsManager.GetTexture("psna.png");

            _psnaWindow = new PsnaWindow(_moduleParameters.ContentsManager, _moduleParameters.Gw2ApiManager, _hideKnownNpcs);


            _cornerIcon = new CornerIcon
            {
                Icon             = icon,
                BasicTooltipText = "Where Is My PSNA",
                Priority         = 845201,
                Parent           = GameService.Graphics.SpriteScreen,
            };

            _cornerIcon.Click += (s, ev) => _psnaWindow.ToggleWindow();

            base.OnModuleLoaded(e);
        }

        protected override void Update(GameTime gameTime) { }

        protected override void Unload()
        {
            _cornerIcon?.Dispose();
            _psnaWindow?.Dispose();
        }
    }
}