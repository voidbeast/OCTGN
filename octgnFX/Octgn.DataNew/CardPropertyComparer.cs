﻿namespace Octgn.DataNew
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using Octgn.DataNew.Entities;

    public class CardPropertyComparer : IComparer, IComparer<Card>
    {
        private readonly bool _isName;
        private readonly string _propertyName;

        public CardPropertyComparer(string propertyName)
        {
            _propertyName = propertyName;
            _isName = propertyName == "Name";
        }

        #region IComparer Members

        int IComparer.Compare(object x, object y)
        {
            return Compare(x as Card, y as Card);
        }

        #endregion

        #region IComparer<CardModel> Members

        public int Compare(Card x, Card y)
        {
            if (_isName)
                return String.CompareOrdinal(x.Name, y.Name);

            object px = x.Properties[new PropertyDef(){Name=_propertyName}];
            object py = y.Properties[new PropertyDef() { Name = _propertyName }];
            if (px == null) return py == null ? 0 : -1;
            return ((IComparable)px).CompareTo(py);
        }

        #endregion
    }
}