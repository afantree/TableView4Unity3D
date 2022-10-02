using TableView.Scripts.Core;

namespace TableView.Scripts.DataSource
{
    public interface ITableViewDataSource
    {
        int NumberOfRowsInTableView(TableView.Scripts.Core.TableView tableView);
        float SizeForRowInTableView(TableView.Scripts.Core.TableView tableView, int row);
        TableViewCell CellForRowInTableView(TableView.Scripts.Core.TableView tableView, int row);
    }
}