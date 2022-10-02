namespace TableView.Scripts.Delegate
{
    public interface ITableViewDelegate
    {
        void TableViewDidHighlightCellForRow(TableView.Scripts.Core.TableView tableView, int row);
        void TableViewDidSelectCellForRow(TableView.Scripts.Core.TableView tableView, int row);
    }
}