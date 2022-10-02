using System;
using UnityEngine.Events;

namespace TableView.Scripts.Core.Events
{
    [Serializable]
    public class TableViewCellDidSelectEvent : UnityEvent<int,bool>
    {

    }
}