using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TableView.Scripts.Core.Content;
using TableView.Scripts.Core.Layout;
using TableView.Scripts.Core.PlaceHolder;
using TableView.Scripts.Core.Prefab;
using TableView.Scripts.Core.Reusable;
using TableView.Scripts.Core.Scroll;
using TableView.Scripts.Core.Sizes;
using TableView.Scripts.Core.Visible;
using TableView.Scripts.DataSource;
using TableView.Scripts.Delegate;
using TableView.Scripts.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace TableView.Scripts.Core
{
    public class TableView : MonoBehaviour, ITableView
    {
        #region Property

        [SerializeField] private TableViewLayoutOrientation layoutOrientation = TableViewLayoutOrientation.Vertical;
        [SerializeField] private float interItemSpacing = 10.0f;
        [SerializeField] public RectOffset padding = new RectOffset();

        [SerializeField] private bool inertia = true;
        [SerializeField] private ScrollRect.MovementType movementType = ScrollRect.MovementType.Elastic;
        [SerializeField] private float elasticity = 0.1f;
        [SerializeField] private float scrollSensitivity = 1.0f;
        [SerializeField] private float decelerationRate = 0.135f;

        [SerializeField, Tooltip("允许选择，默认是单个")]
        private bool allowsSelection = true;

        [SerializeField, Tooltip("允许多个选择")] private bool allowsMultipleSelection;
        [SerializeField, Tooltip("允许清空选择")] private bool allowsSwitchOff = true;

        [SerializeField] private bool scrollToHighlighted = true;
        [SerializeField] private bool clipsToBounds = true;
        [SerializeField] private float scrollingSpeed = 0.2f;

        public ITableViewDataSource DataSource
        {
            get { return dataSource; }
            set
            {
                dataSource = value;
                requiresReload = true;
            }
        }

        public ITableViewDelegate Delegate
        {
            get { return tableViewDelegate; }
            set { tableViewDelegate = value; }
        }

        public UnityEngine.SocialPlatforms.Range VisibleRange
        {
            get { return visibleCells.Range; }
        }

        public float ContentSize
        {
            get { return tableViewScroll.Size - TableViewSize; }
        }

        public float Position
        {
            get { return position; }
        }

        public CellSizes CellSizes;
        private PrefabCells prefabCells;
        private VisibleCells visibleCells;
        private ReusableCells reusableCells;
        private readonly HashSet<int> selectedRows = new HashSet<int>();

        private TableViewScroll tableViewScroll;
        private TableViewLayout tableViewLayout;
        private TableViewContent tableViewContent;
        private TableViewPlaceHolders tableViewPlaceHolders;

        private ITableViewDataSource dataSource;
        private ITableViewDelegate tableViewDelegate;

        private float position;

        private bool isEmpty;
        private bool requiresReload;
        private bool requiresRefresh;

        private bool IsVertical
        {
            get { return (tableViewLayout.Orientation == TableViewLayoutOrientation.Vertical); }
        }

        private float TableViewSize
        {
            get
            {
                Rect rect = ((RectTransform)this.transform).rect;
                return IsVertical ? rect.height : rect.width;
            }
        }

        public PrefabCells GetPrefabCells => prefabCells;

        public VisibleCells GetVisibleCells
        {
            get { return visibleCells; }
        }

        public ReusableCells GetReusableCells
        {
            get { return reusableCells; }
        }

        #endregion


        #region Lifecycle

        void Awake()
        {
            isEmpty = true;

            CellSizes = new CellSizes();
            prefabCells = new PrefabCells();
            visibleCells = new VisibleCells();

            reusableCells = new ReusableCells();
            reusableCells.AddToParentTransform(this.transform as RectTransform);

            tableViewContent = new TableViewContent(this.transform as RectTransform);
            tableViewContent.SetLayoutOrientation(layoutOrientation);

            tableViewScroll = new TableViewScroll(this.gameObject, tableViewContent.Container);
            tableViewScroll.SetLayoutOrientation(layoutOrientation);
            tableViewScroll.Elasticity = elasticity;
            tableViewScroll.MovementType = movementType;
            tableViewScroll.Inertia = inertia;
            tableViewScroll.DecelerationRate = decelerationRate;
            tableViewScroll.ScrollSensitivity = scrollSensitivity;

            tableViewPlaceHolders = new TableViewPlaceHolders(tableViewScroll.Content);
            tableViewPlaceHolders.SetLayoutOrientation(layoutOrientation);

            tableViewLayout = new TableViewLayout(layoutOrientation);
            tableViewLayout.AddToParent(tableViewScroll.Content.gameObject);
            tableViewLayout.Spacing = interItemSpacing;
            tableViewLayout.Padding = padding;

            if (this.clipsToBounds)
            {
                this.gameObject.AddComponent<RectMask2D>();
            }

            this.gameObject.AddComponent<CanvasRenderer>();
        }

        void Update()
        {
            if (requiresReload)
            {
                ReloadData();
            }
        }

        void LateUpdate()
        {
            if (requiresRefresh)
            {
                RefreshVisibleCells();
            }
        }

        void OnEnable()
        {
            tableViewScroll.OnValueChanged.AddListener(ScrollViewValueChanged);
        }

        void OnDisable()
        {
            tableViewScroll.OnValueChanged.RemoveListener(ScrollViewValueChanged);
        }

        #endregion


        #region Public

        public void CellDidSelect(int row, bool isIn = false)
        {
            if (!allowsSelection)
            {
                return;
            }

            if (tableViewDelegate != null)
            {
                tableViewDelegate.TableViewDidSelectCellForRow(this, row);
            }

            if (allowsMultipleSelection)
            {
                if (selectedRows.Contains(row))
                {
                    DeselectCellAtRow(row);
                }
                else
                {
                    SelectCellAtRow(row, isIn);
                }
            }
            else
            {
                if (selectedRows.Count > 0)
                {
                    if (selectedRows.Contains(row))
                    {
                        if (allowsSwitchOff)
                        {
                            DeselectCellAtRow(row);
                        }
                    }
                    else
                    {
                        int oldRow = selectedRows.ToArray()[0];
                        DeselectCellAtRow(oldRow);
                        selectedRows.Clear();
                        SelectCellAtRow(row, isIn);
                    }
                }
                else
                {
                    SelectCellAtRow(row, isIn);
                }
            }
        }

        public void ResetState()
        {
            selectedRows.Clear();
        }

        public TableViewCell ReusableCellForRow(string reuseIdentifier, int row)
        {
            TableViewCell cell = reusableCells.GetReusableCell(reuseIdentifier);

            if (cell == null)
            {
                cell = CreateCellFromPrefab(reuseIdentifier, row);
            }

            cell.RestoreState();
            return cell;
        }

        public TableViewCell CellForRow(int row)
        {
            return visibleCells.GetCellAtIndex(row);
        }

        public float PositionForRow(int row)
        {
            return CellSizes.GetCumulativeRowSize(row) - CellSizes.SizeForRow(row) + tableViewLayout.StartPadding;
        }

        public void ReloadData()
        {
            int numberOfRows = dataSource.NumberOfRowsInTableView(this);
            CellSizes.SetRowsCount(numberOfRows);
            isEmpty = (numberOfRows == 0);
            RemoveAllCells();

            if (isEmpty) return;

            for (int i = 0; i < numberOfRows; i++)
            {
                float rowSize = dataSource.SizeForRowInTableView(this, i) + tableViewLayout.Spacing;
                CellSizes.SetSizeForRow(rowSize, i);
            }

            tableViewScroll.SizeDelta = CellSizes.GetCumulativeRowSize(numberOfRows - 1) + tableViewLayout.EndPadding;

            CreateCells();
            requiresReload = false;
        }

        public void RegisterPrefabForCellReuseIdentifier(GameObject prefab, string cellReuseIdentifier)
        {
            prefabCells.RegisterPrefabForCellReuseIdentifier(prefab, cellReuseIdentifier);
        }

        public void SetPosition(float newPosition)
        {
            if (isEmpty)
            {
                return;
            }

            newPosition = Mathf.Clamp(newPosition, 0, PositionForRow(CellSizes.RowsCount - 1));

            if (Math.Abs(position - newPosition) > 0.01f)
            {
                position = newPosition;
                requiresRefresh = true;
                float normalizedPosition = newPosition / ContentSize;
                float relativeScroll;

                if (IsVertical)
                {
                    relativeScroll = 1 - normalizedPosition;
                }
                else
                {
                    relativeScroll = normalizedPosition;
                }

                tableViewScroll.SetNormalizedPosition(relativeScroll);
            }
        }

        public void SetPosition(float newPosition, float time)
        {
            StartCoroutine(AnimateToPosition(newPosition, time));
        }

        #endregion


        #region Private

        public void SelectCellAtRow(int row, bool isIn = false)
        {
            selectedRows.Add(row);
            if (!isIn)
            {
                var cell = CellForRow(row);
                if (cell)
                {
                    cell.SetSelected();
                }
            }
        }

        public void DeselectCellAtRow(int row)
        {
            selectedRows.Remove(row);
            var cell = CellForRow(row);
            if (cell)
            {
                cell.SetDeselected();
            }
        }

        private TableViewCell CreateCellFromPrefab(string reuseIdentifier, int row)
        {
            if (!prefabCells.IsRegisteredCellReuseIdentifier(reuseIdentifier)) return null;

            GameObject cellPrefab = prefabCells.PrefabForCellReuseIdentifier(reuseIdentifier);
            TableViewCell cell = Instantiate(cellPrefab).GetComponent<TableViewCell>();
            cell.SetRowNumber(row);

            return ConfigureCellWithRowAtEnd(cell, row, true);
        }

        private void ScrollViewValueChanged(Vector2 newScrollValue)
        {
            float relativeScroll;

            if (IsVertical)
            {
                relativeScroll = 1 - newScrollValue.y;
            }
            else
            {
                relativeScroll = newScrollValue.x;
            }

            position = relativeScroll * ContentSize;
            requiresRefresh = true;
        }

        private void CreateCells()
        {
            RemoveAllCells();
            SetInitialVisibleCells();
        }

        private void RemoveAllCells()
        {
            while (visibleCells.Count > 0)
            {
                RemoveCell(false);
            }

            visibleCells.Range = new UnityEngine.SocialPlatforms.Range(0, 0);
        }

        private UnityEngine.SocialPlatforms.Range CurrentVisibleCellsRange()
        {
            float startPosition = Math.Max(position - tableViewLayout.StartPadding - TableViewSize, 0);
            float endPosition = position + (TableViewSize * 2.0f);

            if (endPosition > tableViewScroll.Size)
            {
                endPosition = tableViewScroll.Size;
            }

            int startIndex = CellSizes.FindIndexOfRowAtPosition(startPosition);
            int endIndex = CellSizes.FindIndexOfRowAtPosition(endPosition);

            if (endIndex == CellSizes.RowsCount - 1)
            {
                endIndex = CellSizes.RowsCount;
            }

            int cellsCount = endIndex - startIndex;

            return new UnityEngine.SocialPlatforms.Range(startIndex, cellsCount);
        }

        private void SetInitialVisibleCells()
        {
            UnityEngine.SocialPlatforms.Range currentRange = CurrentVisibleCellsRange();

            for (int i = 0; i < currentRange.count; i++)
            {
                CreateCell(currentRange.from + i, true);
            }

            visibleCells.Range = currentRange;
            UpdatePaddingElements();
        }

        private void RefreshVisibleCells()
        {
            requiresRefresh = false;

            if (isEmpty) return;

            UnityEngine.SocialPlatforms.Range previousRange = visibleCells.Range;
            UnityEngine.SocialPlatforms.Range currentRange = CurrentVisibleCellsRange();

            if (currentRange.from > previousRange.Last() || currentRange.Last() < previousRange.from)
            {
                CreateCells();
                return;
            }

            RemoveCellsIfNeededWithRanges(previousRange, currentRange);
            CreateCellsIfNeededWithRanges(previousRange, currentRange);

            visibleCells.Range = currentRange;

            UpdatePaddingElements();
        }

        private void RemoveCellsIfNeededWithRanges(UnityEngine.SocialPlatforms.Range previousRange,
            UnityEngine.SocialPlatforms.Range currentRange)
        {
            for (int i = previousRange.from; i < currentRange.from; i++)
            {
                RemoveCell(false);
            }

            for (int i = currentRange.Last(); i < previousRange.Last(); i++)
            {
                RemoveCell(true);
            }
        }

        private void CreateCellsIfNeededWithRanges(UnityEngine.SocialPlatforms.Range previousRange,
            UnityEngine.SocialPlatforms.Range currentRange)
        {
            for (int i = previousRange.from - 1; i >= currentRange.from; i--)
            {
                CreateCell(i, false);
            }

            for (int i = previousRange.Last() + 1; i <= currentRange.Last(); i++)
            {
                CreateCell(i, true);
            }
        }

        private void CreateCell(int row, bool atEnd)
        {
            TableViewCell cell = dataSource.CellForRowInTableView(this, row);
            ConfigureCellWithRowAtEnd(cell, row, atEnd);
        }

        private TableViewCell ConfigureCellWithRowAtEnd(TableViewCell cell, int row, bool atEnd)
        {
            cell.SetRowNumber(row);
            cell.transform.SetParent(tableViewScroll.Content, false);

            float cellSize = CellSizes.SizeForRow(row) - tableViewLayout.Spacing;
            CreateLayoutIfNeededForCellWithSize(cell, cellSize);

            cell.DidHighlightEvent.RemoveListener(CellDidHighlight);
            cell.DidHighlightEvent.AddListener(CellDidHighlight);

            cell.DidSelectEvent.RemoveListener(CellDidSelect);
            cell.DidSelectEvent.AddListener(CellDidSelect);

            visibleCells.SetCellAtIndex(row, cell);

            if (atEnd)
            {
                cell.transform.SetSiblingIndex(tableViewScroll.Content.childCount - 2);
            }
            else
            {
                cell.transform.SetSiblingIndex(1);
            }

            return cell;
        }

        private void RemoveCell(bool last)
        {
            int row = last ? visibleCells.Range.Last() : visibleCells.Range.from;
            TableViewCell removedCell = visibleCells.GetCellAtIndex(row);
            removedCell.DidHighlightEvent.RemoveListener(CellDidHighlight);
            reusableCells.AddReusableCell(removedCell);
            visibleCells.RemoveCellAtIndex(row);
            visibleCells.Range.count -= 1;

            if (!last)
            {
                visibleCells.Range.from += 1;
            }
        }

        private void CreateLayoutIfNeededForCellWithSize(TableViewCell cell, float size)
        {
            LayoutElement layoutElement = cell.GetComponent<LayoutElement>();

            if (layoutElement == null)
            {
                layoutElement = cell.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = 0;
            layoutElement.preferredWidth = 0;

            if (IsVertical)
            {
                layoutElement.preferredHeight = size;
            }
            else
            {
                layoutElement.preferredWidth = size;
            }
        }

        private void UpdatePaddingElements()
        {
            UnityEngine.SocialPlatforms.Range startRange =
                new UnityEngine.SocialPlatforms.Range(0, visibleCells.Range.from);
            float startSize = CellSizes.SumWithRange(startRange);

            UnityEngine.SocialPlatforms.Range endRange =
                new UnityEngine.SocialPlatforms.Range(visibleCells.Range.from, visibleCells.Range.Last() + 1);
            float hiddenElementsSizeSum = startSize + CellSizes.SumWithRange(endRange);
            float endSize = tableViewScroll.Size - hiddenElementsSizeSum;
            endSize -= tableViewLayout.StartPadding;
            endSize -= tableViewLayout.EndPadding;
            endSize -= tableViewLayout.Spacing;

            tableViewPlaceHolders.UpdatePlaceHoldersWithSize(startSize, endSize);
        }

        private IEnumerator AnimateToPosition(float newPosition, float time)
        {
            float startTime = Time.time;
            float initialPosition = position;
            float endTime = startTime + time;

            while (Time.time < endTime)
            {
                float relativeProgress = Mathf.InverseLerp(startTime, endTime, Time.time);
                SetPosition(Mathf.Lerp(initialPosition, newPosition, relativeProgress));
                yield return new WaitForEndOfFrame();
            }

            SetPosition(newPosition);
        }

        private void CellDidHighlight(int row)
        {
            if (tableViewDelegate != null)
            {
                tableViewDelegate.TableViewDidHighlightCellForRow(this, row);
            }

            if (scrollToHighlighted)
            {
                MakeVisibleRowIfNeeded(row);
            }
        }


        private void MakeVisibleRowIfNeeded(int row)
        {
            ScrollToStartIfNeededWithRow(row);
            ScrollToEndIfNeededWithRow(row);
        }

        public void ScrollToStartIfNeededWithRow(int row)
        {
            float rowStart = PositionForRow(row);

            if (row == 0)
            {
                rowStart -= tableViewLayout.StartPadding;
            }

            if (position > rowStart)
            {
                SetPosition(rowStart, scrollingSpeed);
            }
        }

        public void ScrollToEndIfNeededWithRow(int row)
        {
            float rowStart = PositionForRow(row);
            float rowSize = CellSizes.SizeForRow(row);
            float rowEnd = rowStart + rowSize + tableViewLayout.Spacing;
            float contentEndPosition = position + TableViewSize;
            int rowsCount = CellSizes.RowsCount - 1;

            if (row == rowsCount)
            {
                rowEnd += tableViewLayout.EndPadding;
            }

            if (rowEnd > tableViewScroll.Size)
            {
                rowEnd = tableViewScroll.Size;
            }

            if (rowEnd > contentEndPosition)
            {
                float rowEndPosition = rowEnd - TableViewSize;
                SetPosition(rowEndPosition, scrollingSpeed);
            }
        }

        /*** 新加的方法 如果有问题再优化 ***/
        public void ScrollToStart(float speed)
        {
            float rowStart = PositionForRow(0);
            rowStart -= tableViewLayout.StartPadding;
            if (position > rowStart)
            {
                SetPosition(rowStart, speed);
            }
        }

        public void ScrollToEnd(float speed)
        {
            float contentEndPosition = position + TableViewSize;
            float rowEnd = tableViewScroll.Size;
            if (rowEnd > contentEndPosition)
            {
                float rowEndPosition = rowEnd - TableViewSize;
                SetPosition(rowEndPosition, speed);
            }
        }

        #endregion
    }
}