
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
// REMOVA ESTA LINHA:
using CS2MenuManager.API.Menu;


namespace AdminControlPlugin.commands
{
    internal class GraphicalMenu
    {
        private string v;
        private AdminControlPlugin plugin;

        public GraphicalMenu(string v, AdminControlPlugin plugin)
        {
            this.v = v;
            this.plugin = plugin;
        }

        public bool ExitButton { get; set; }
        public int MenuTime { get; set; }

        internal void AddItem(string v, Action<object, object> value)
        {
            throw new NotImplementedException();
        }

        internal void AddMenuItem(string v, Action<object, object> value)
        {
            throw new NotImplementedException();
        }

        internal void Display(CCSPlayerController caller, int v)
        {
            throw new NotImplementedException();
        }
    }
}