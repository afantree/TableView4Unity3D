using TableView.Scripts.Core;
using UnityEngine.UI;

namespace TableView.Samples
{
    public class ExampleTableViewCell : TableViewCell
    {
        public Text text;

        public override string ReuseIdentifier
        {
            get { return "ExampleTableViewCellReuseIdentifier"; }
        }

        public override void SetHighlighted()
        {
            print("CellSetHighlighted : " + RowNumber);
        }

        public override void SetSelected()
        {
            print("CellSetSelected : " + RowNumber);
        }

        public override void Display()
        {
            text.text = "Row " + RowNumber;
        }

        public override void RestoreState()
        {
            
        }

        public override void SetDeselected()
        {
            
        }
    }
}