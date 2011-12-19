﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Octgn.Controls;
using System.IO;
using Skylabs.Lobby;

namespace Octgn.Launcher
{
    /// <summary>
    /// Interaction logic for GameList.xaml
    /// </summary>
    public partial class HostedGameList : Page
    {
        public event EventHandler OnGameClick;
        public HostedGameList()
        {
            InitializeComponent();
            Program.lobbyClient.OnGameHostEvent += new Skylabs.Lobby.LobbyClient.GameHostEvent(lobbyClient_OnGameHostEvent);
        }

        void lobbyClient_OnGameHostEvent(Skylabs.Lobby.HostedGame g)
        {
            Reload_Game_List();
        }
        private void Reload_Game_List()
        {
            this.Dispatcher.Invoke(new Action(() =>
                                                  {
                                                      stackPanel1.Children.Clear();
                                                      foreach (HostedGame g in Program.lobbyClient.Games)
                                                      {
                                                          HostedGameListItem gs = new HostedGameListItem(g);
                                                          if(g.GameStatus == HostedGame.eHostedGame.StartedHosting)
                                                            gs.MouseUp += new MouseButtonEventHandler(gs_MouseUp);
                                                          stackPanel1.Children.Add(gs);
                                                      }                                                                  
                                                  }));
        }

        void gs_MouseUp(object sender, MouseButtonEventArgs e)
        {
            HostedGameListItem gs = (HostedGameListItem)sender;
            if (OnGameClick != null)
            {
                OnGameClick.Invoke(gs.Game, new EventArgs());
            }
        }
        void GamesRepository_GameInstalled(object sender, EventArgs e)
        {
            Reload_Game_List();
        }

        void gs_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            HostedGameListItem gs = (HostedGameListItem)sender;
            if (OnGameClick != null)
            {
                OnGameClick.Invoke(gs.Game,new EventArgs());
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {

            Reload_Game_List();
        }
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Program.lobbyClient.OnGameHostEvent -= lobbyClient_OnGameHostEvent;
        }
    }
}
