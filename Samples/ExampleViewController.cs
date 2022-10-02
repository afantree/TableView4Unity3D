using TableView.Scripts.Core;
using TableView.Scripts.DataSource;
using TableView.Scripts.Delegate;
using UnityEngine;

namespace TableView.Samples
{
    public class ExampleViewController : MonoBehaviour, ITableViewDataSource, ITableViewDelegate
    {
        public Scripts.Core.TableView customTableView;
        public GameObject cell;
        void Start()
        {
            customTableView.Delegate = this;
            customTableView.DataSource = this;
            
            customTableView.RegisterPrefabForCellReuseIdentifier(cell, "ExampleTableViewCellReuseIdentifier");
        }

        public int NumberOfRowsInTableView(Scripts.Core.TableView tableView)
        {
            return 100;
        }

        public float SizeForRowInTableView(Scripts.Core.TableView tableView, int row)
        {
            return Random.Range(50.0f, 200.0f);
        }

        public TableViewCell CellForRowInTableView(Scripts.Core.TableView tableView, int row)
        {
            TableViewCell cell = tableView.ReusableCellForRow("ExampleTableViewCellReuseIdentifier", row);
            cell.name = "Cell " + row;
            return cell;
        }

        public void TableViewDidHighlightCellForRow(Scripts.Core.TableView tableView, int row)
        {
            print("TableViewDidHighlightCellForRow : " + row);
        }

        public void TableViewDidSelectCellForRow(Scripts.Core.TableView tableView, int row)
        {
            print("TableViewDidSelectCellForRow : " + row);
        }
    }
}