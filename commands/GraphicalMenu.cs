
using System;
using CounterStrikeSharp.API.Core;
using CS2MenuManager.API.Menu;

namespace AdminControlPlugin.commands
{
    public class GraphicalMenu
    {
        private readonly PlayerMenu _menu;

        public GraphicalMenu(string title, AdminControlPlugin plugin)
        {
            _menu = new PlayerMenu(title, plugin);
        }

        public bool ExitButton
        {
            get => _menu.ExitButton;
            set => _menu.ExitButton = value;
        }

        public int MenuTime
        {
            get => _menu.MenuTime;
            set => _menu.MenuTime = value;
        }

        internal void AddItem(string text, Action<object, object> callback)
        {
            _menu.AddItem(text, (player, option) => {
                callback(player, option);
            });
        }

        internal void AddMenuItem(string text, Action<object, object> callback)
        {
            _menu.AddItem(text, (player, option) => {
                callback(player, option);
            });
        }

        internal void Display(CCSPlayerController caller, int v)
        {
            _menu.Display(caller, v);
        }
    }
}