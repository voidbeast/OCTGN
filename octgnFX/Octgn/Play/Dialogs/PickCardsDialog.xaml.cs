﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Octgn.Controls;
using Octgn.Data;
using Octgn.Definitions;
using Octgn.Play.Gui;
using Octgn.Utils;

namespace Octgn.Play.Dialogs
{
    using System.Threading.Tasks;

    using Octgn.Core.DataExtensionMethods;
    using Octgn.DataNew;
    using Octgn.DataNew.Entities;

    public partial class PickCardsDialog
    {
        public PickCardsDialog()
        {
            CardPool = new ObservableCollection<DataNew.Entities.Card>();
            CardPoolView = new ListCollectionView(CardPool) {Filter = FilterCard};
            UnlimitedPool = new ObservableCollection<DataNew.Entities.Card>();
            UnlimitedPoolView = new ListCollectionView(UnlimitedPool);
            UnlimitedPoolView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            LimitedDeck = Database.OpenedGame.CreateDeck();
            CreateFilters();
            InitializeComponent();
        }

        public ListCollectionView CardPoolView { get; private set; }
        public ObservableCollection<DataNew.Entities.Card> CardPool { get; private set; }
        public ListCollectionView UnlimitedPoolView { get; private set; }
        public ObservableCollection<DataNew.Entities.Card> UnlimitedPool { get; private set; }
        public Deck LimitedDeck { get; private set; }
        public ObservableCollection<Filter> Filters { get; private set; }

        private void SortChanged(object sender, SelectionChangedEventArgs e)
        {
            CardPoolView.CustomSort = new CardPropertyComparer(((PropertyDef) sortBox.SelectedItem).Name);
        }

        public void OpenPacks(IEnumerable<Guid> packs)
        {
            new Task(
                () =>
                    {
                        Dispatcher.Invoke(new Action(() =>
                            {
                                TabControlMain.IsEnabled = false;
                                ProgressBarLoading.Visibility = Visibility.Visible;
                                ProgressBarLoading.IsIndeterminate = true;
                                ProgressBarLoading.Maximum = packs.Count();
                            }));
                        this.StopListenningForFilterValueChanges();

                        foreach (Pack pack in packs.Select(Database.GetPackById))
                        {
                            if (pack == null)
                            {
                                Program.TraceWarning("Received pack is missing from the database. Pack is ignored.");
                                continue;
                            }

                            PackContent content = pack.CrackOpen();
                            foreach (DataNew.Entities.Card c in content.LimitedCards)
                            {
                                Dispatcher.Invoke(new Action(() => { this.CardPool.Add(c); }));
                            }

                            foreach (DataNew.Entities.Card c in content.UnlimitedCards)
                            {
                                Dispatcher.Invoke(new Action(() => { this.UnlimitedPool.Add(c); }));
                            }

                            Dispatcher.Invoke(new Action(() =>
                                {
                                    ProgressBarLoading.IsIndeterminate = false;
                                    ProgressBarLoading.Value += 1;
                                }));
                        }

                        Dispatcher.Invoke(
                            new Action(() =>
                                {
                                    this.UpdateFilters();

                                    this.ListenForFilterValueChanges();
                                    ProgressBarLoading.Visibility = Visibility.Hidden;
                                    TabControlMain.IsEnabled = true;
                                }));
                    }).Start();
        }

        private void ComputeChildWidth(object sender, RoutedEventArgs e)
        {
            var panel = sender as VirtualizingWrapPanel;
            CardDef cardDef = Program.Game.Definition.CardDefinition;
            if (panel != null) panel.ChildWidth = panel.ChildHeight*cardDef.Width/cardDef.Height;
        }

        private void SetPicture(object sender, RoutedEventArgs e)
        {
            var img = sender as Image;
            if (img == null) return;
            var model = img.DataContext as DataNew.Entities.Card ?? (img.DataContext as MultiCard);
            if (model == null) return;
            ImageUtils.GetCardImage(new Uri(model.GetPicture()), x => img.Source = x);
        }

        private void PickPoolCard(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource as FrameworkElement;
            if (src == null) return;
            var card = src.DataContext as DataNew.Entities.Card;
            if (card == null) return;
            var section = deckTabs.SelectedItem as Section;
            if (section == null) return;

            CardPool.Remove(card);
            MultiCard element = section.Cards.FirstOrDefault(x => x.Id == card.Id);
            if (element != null)
                element.Quantity++;
            else
                section.Cards.Add(card.ToMultiCard());

            // RaiseEvent(new CardEventArgs(CardControl.CardHoveredEvent, sender));
        }

        private void PickUnlimitedCard(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource as FrameworkElement;
            if (src == null) return;
            var card = src.DataContext as DataNew.Entities.Card;
            if (card == null) return;
            var section = deckTabs.SelectedItem as Section;
            if (section == null) return;

            // Hide the big card preview
            RaiseEvent(new CardEventArgs(CardControl.CardHoveredEvent, sender));

            OpenQuantityPopup(qty =>
                                  {
                                      MultiCard element = section.Cards.FirstOrDefault(x => x.Id == card.Id);
                                      if (element != null)
                                          element.Quantity += (byte) qty;
                                      else
                                          section.Cards.Add(card.ToMultiCard((byte) qty));
                                  });
        }

        private void RemoveDeckCard(object sender, RoutedEventArgs e)
        {
            var src = e.OriginalSource as FrameworkElement;
            if (src == null) return;
            var element = (src.DataContext as MultiCard);
            if (element == null) return;
            var section = deckTabs.SelectedItem as Section;

            if (element.Quantity > 1)
            {
                OpenQuantityPopup(qty =>
                                      {
                                          int actuallyRemoved = Math.Min(qty, element.Quantity);
                                          if (element.Quantity > qty)
                                              element.Quantity -= (byte) qty;
                                          else if (section != null) section.Cards.Remove(element);
                                          if (!UnlimitedPool.Contains(element))
                                              for (int i = 0; i < actuallyRemoved; ++i)
                                                  // When there are multiple copies of the same card, we insert clones of the CardModel.
                                                  // Otherwise, the ListBox gets confused with selection.
                                                  CardPool.Add(element.Quantity > 1
                                                                   ? element.Clone()
                                                                   : element);
                                      });
            }
            else
            {
                if (element.Quantity > 1)
                    element.Quantity--;
                else if (section != null) section.Cards.Remove(element);
                if (!UnlimitedPool.Contains(element))
                    CardPool.Add(element);
            }
        }

        private void MouseEnterCard(object sender, MouseEventArgs e)
        {
            var src = e.Source as FrameworkElement;
            if (src == null) return;
            var model = src.DataContext as DataNew.Entities.Card ?? (src.DataContext as MultiCard);
            if (model == null) return;
            RaiseEvent(new CardEventArgs(model, CardControl.CardHoveredEvent, sender));
        }

        private void MouseLeaveCard(object sender, MouseEventArgs e)
        {
            RaiseEvent(new CardEventArgs(CardControl.CardHoveredEvent, sender));
        }

        #region Filters

        private readonly List<FilterValue> _activeRestrictions = new List<FilterValue>();

        private bool FilterCard(object c)
        {
            if (_activeRestrictions == null || _activeRestrictions.Count == 0)
                return true;
            var card = (DataNew.Entities.Card) c;

            return (from restriction in _activeRestrictions.GroupBy(fv => fv.Property)
                    let prop = restriction.Key
                    let value = card.Properties[prop]
                    select restriction.Any(filterValue => filterValue.IsValueMatch(value))).All(isOk => isOk);
        }

        private void CreateFilters()
        {
            Filters = new ObservableCollection<Filter>();
            foreach (Filter filter in Program.Game.Definition.CardDefinition.Properties.Values
                .Where(p => !p.Hidden && (p.Type == PropertyType.Integer || p.TextKind != PropertyTextKind.FreeText)).
                Select(prop => new Filter
                                   {
                                       Name = prop.Name,
                                       Property = prop,
                                       Values = new ObservableCollection<FilterValue>()
                                   }))
            {
                Filters.Add(filter);
            }
        }

        private void StopListenningForFilterValueChanges()
        {
            CardPool.CollectionChanged -= CardPoolChanged;
        }

        private void ListenForFilterValueChanges()
        {
            CardPool.CollectionChanged += CardPoolChanged;
        }

        private void UpdateFilters()
        {
            _activeRestrictions.Clear();
            foreach (Filter filter in Filters)
            {
                PropertyDef prop = filter.Property;
                filter.Values.Clear();
                if (prop.Type == PropertyType.String)
                    switch (prop.TextKind)
                    {
                        case PropertyTextKind.Enumeration:
                            Filter filter2 = filter;
                            IEnumerable<EnumFilterValue> q = from DataNew.Entities.Card c in CardPoolView
                                                             group c by (string) c.Properties[prop]
                                                             into g
                                                             orderby g.Key
                                                             select
                                                                 new EnumFilterValue
                                                                     {
                                                                         Property = filter2.Property,
                                                                         Value = g.Key,
                                                                         Count = g.Count()
                                                                     };
                            foreach (EnumFilterValue filterValue in q)
                                filter.Values.Add(filterValue);
                            break;
                        case PropertyTextKind.Tokens:
                            Filter filter1 = filter;
                            IEnumerable<TokenFilterValue> q2 = from DataNew.Entities.Card c in CardPoolView
                                                               let all = (string) c.Properties[prop]
                                                               where all != null
                                                               from token in
                                                                   all.Split(new[] {' '},
                                                                             StringSplitOptions.RemoveEmptyEntries)
                                                               group c by token
                                                               into g
                                                               orderby g.Key
                                                               select
                                                                   new TokenFilterValue
                                                                       {
                                                                           Property = filter1.Property,
                                                                           Value = g.Key,
                                                                           Count = g.Count()
                                                                       };
                            foreach (TokenFilterValue filterValue in q2)
                                filter.Values.Add(filterValue);
                            break;
                    }
            }
        }

        private void CardPoolChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (FilterValue fv in Filters.SelectMany(f => f.Values))
            {
                if (e.NewItems != null)
                {
                    FilterValue fv1 = fv;
                    foreach (DataNew.Entities.Card card in e.NewItems.Cast<DataNew.Entities.Card>().Where(fv1.IsMatch))
                        fv.Count++;
                }
                if (e.OldItems != null)
                {
                    FilterValue fv1 = fv;
                    foreach (DataNew.Entities.Card card in e.OldItems.Cast<DataNew.Entities.Card>().Where(fv1.IsMatch))
                        fv.Count--;
                }
            }
        }

        private void FilterChecked(object sender, RoutedEventArgs e)
        {
            var src = ((FrameworkElement) sender);
            var filterValue = (FilterValue) src.DataContext;
            _activeRestrictions.Add(filterValue);
            CardPoolView.Refresh();
        }

        private void FilterUnchecked(object sender, RoutedEventArgs e)
        {
            var src = ((FrameworkElement) sender);
            var filterValue = (FilterValue) src.DataContext;
            _activeRestrictions.Remove(filterValue);
            CardPoolView.Refresh();
        }

        #endregion

        #region Quantity popup

        private Action<int> _quantityPopupAction;

        private void OpenQuantityPopup(Action<int> qtyAction)
        {
            _quantityPopupAction = qtyAction;
            quantityPopup.IsOpen = true;
            quantityBox.Text = "1";
            quantityBox.Focus();
            quantityBox.SelectAll();
        }

        private void QuantityBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    int qty;
                    if (!int.TryParse(quantityBox.Text, out qty)) return;
                    if (qty < 1) return;
                    _quantityPopupAction(qty);
                    quantityPopup.IsOpen = false;
                    break;
                case Key.Escape:
                    e.Handled = true;
                    quantityPopup.IsOpen = false;
                    break;
            }
        }

        private void QuantityBoxLostFocus(object sender, RoutedEventArgs e)
        {
            quantityPopup.IsOpen = false;
        }

        #endregion

        #region Nested type: EnumFilterValue

        public class EnumFilterValue : FilterValue
        {
            public string Value { get; set; }

            public override bool IsValueMatch(object value)
            {
                return (value as string) == Value;
            }
        }

        #endregion

        #region Nested type: Filter

        public class Filter
        {
            public string Name { get; set; }
            public DataNew.Entities.PropertyDef Property { get; set; }
            public ObservableCollection<FilterValue> Values { get; set; }
        }

        #endregion

        #region Nested type: FilterValue

        public abstract class FilterValue : INotifyPropertyChanged
        {
            private int _count;
            public DataNew.Entities.PropertyDef Property { get; set; }
            public bool IsActive { get; set; }

            public int Count
            {
                get { return _count; }
                set
                {
                    if (value == _count) return;
                    _count = value;
                    OnPropertyChanged("Count");
                }
            }

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            #endregion

            public abstract bool IsValueMatch(object value);

            public bool IsMatch(DataNew.Entities.Card c)
            {
                return IsValueMatch(c.Properties[Property]);
            }

            protected void OnPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region Nested type: TokenFilterValue

        public class TokenFilterValue : FilterValue
        {
            private Regex _regex;
            private string _value;

            public string Value
            {
                get { return _value; }
                set
                {
                    _value = value;
                    _regex = new Regex(@"(^|\s)" + value + @"($|\s)", RegexOptions.Compiled);
                }
            }

            public override bool IsValueMatch(object value)
            {
                var strValue = value as string;
                return strValue != null && _regex.IsMatch(strValue);
            }
        }

        #endregion
    }

    public class FilterTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var ctrl = container as FrameworkElement;
            var filter = item as PickCardsDialog.Filter;
            if (filter == null) return base.SelectTemplate(item, container);

            switch (filter.Property.Type)
            {
                case PropertyType.String:
                    if (ctrl != null) return ctrl.FindResource("TextTemplate") as DataTemplate;
                    break;
                case PropertyType.Integer:
                    if (ctrl != null) return ctrl.FindResource("IntTemplate") as DataTemplate;
                    break;
            }
            throw new InvalidOperationException("Unexpected property type: " + filter.Property.Type);
        }
    }
}