using Newtonsoft.Json;
using SkyTrack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SkyWest.Common.Interfaces;
using Binding = System.Windows.Data.Binding;
using Clipboard = System.Windows.Clipboard;
using Colors = System.Windows.Media.Colors;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Cursors = System.Windows.Input.Cursors;
using DataGrid = System.Windows.Controls.DataGrid;
using DataGridCell = System.Windows.Controls.DataGridCell;
using ListView = System.Windows.Forms.ListView;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using SkyWest.Common.Templates;
using SkyWest.Common.Templates.Models;
using SkyWest.Common.Templates.Utilities;
using MyExtensions;
using System.Threading.Tasks;


namespace SkyWest.Common.WPF
{
    /// <summary>
    /// Interaction logic for SkyWestDataGrid.xaml. Once integrated, the datagrid can be sorted and if refreshed, 
    /// it can be resorted to the original state by calling the RestoreGroupAndSort method.
    /// </summary>

    // used for allowing tooltip when hovering above a cell.

    public partial class CollapsibleSkyWestDataGrid : DataGrid, INotifyPropertyChanged
    {
        public class ReflectionUtility
        {
            internal static Func<object, object> GetGetter(PropertyInfo property)
            {
                // get the get method for the property
                MethodInfo method = property.GetGetMethod(true);

                // get the generic get-method generator (ReflectionUtility.GetSetterHelper<TTarget, TValue>)
                MethodInfo genericHelper = typeof(ReflectionUtility).GetMethod(
                    "GetGetterHelper",
                    BindingFlags.Static | BindingFlags.NonPublic);

                // reflection call to the generic get-method generator to generate the type arguments
                MethodInfo constructedHelper = genericHelper.MakeGenericMethod(
                    method.DeclaringType,
                    method.ReturnType);

                // now call it. The null argument is because it's a static method.
                object ret = constructedHelper.Invoke(null, new object[] { method });

                // cast the result to the action delegate and return it
                return (Func<object, object>)ret;
            }

            static Func<object, object> GetGetterHelper<TTarget, TResult>(MethodInfo method)
                where TTarget : class // target must be a class as property sets on structs need a ref param
            {
                // Convert the slow MethodInfo into a fast, strongly typed, open delegate
                Func<TTarget, TResult> func = (Func<TTarget, TResult>)Delegate.CreateDelegate(typeof(Func<TTarget, TResult>), method);

                // Now create a more weakly typed delegate which will call the strongly typed one
                Func<object, object> ret = (object target) => (TResult)func((TTarget)target);
                return ret;
            }
        }


        #region Fields
        private SolidColorBrush defaultBackGroundBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFF0F0F0");
        private SolidColorBrush defaultForeGroundBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF000000");
        private DataGrid sourceDataGrid;
        private ICollectionView collectionView;
        private bool _ignoreFilterChanged;
        private bool _rememberColumnLayout;
        #endregion Fields

        #region Constructors
        public CollapsibleSkyWestDataGrid()
        {
            ClearFilterOnItemSourceChange = true;
            InitializeComponent();

            mnuColorKey.IsEnabled = (!string.IsNullOrEmpty(ShowColorKey));
        }
        #endregion Constructors

        #region Events
        public event EventHandler<EventArgs> ItemsSourceFilterChanged;
        public event EventHandler<MouseButtonEventArgs> RowPreviewMouseDoubleClick;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool ClickedRowDetails = false;
        public int prevSelectedIndex = -1;
        public List<DataGrid> OpenedDataGrids = new List<DataGrid>();

        private async void DataGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //wait for other events to fire
            await Task.Delay(1);

            if (!ClickedRowDetails)
            {
                if (((DataGrid)sender).SelectedItem == null)
                    return;

                //unselect all items in the inner datagrids and remove them from the list that checks them
                //foreach (DataGrid grid in OpenedDataGrids)
                //{
                //    grid.SelectedIndex = -1;
                //}

                //OpenedDataGrids.Clear();

                if (((DataGrid)sender).SelectedIndex == prevSelectedIndex)
                {
                    ((DataGrid)sender).SelectedIndex = -1;
                }

                prevSelectedIndex = ((DataGrid)sender).SelectedIndex;
            }

            ClickedRowDetails = false;
        }

        private void ContentControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            object original = e.OriginalSource;

            if (!original.GetType().Equals(typeof(Border)))
            {
                if (original.GetType().Equals(typeof(TextBlock)))
                {
                    if (((TextBlock)original).DataContext.ToString() != ParentRowDataContext)
                    {
                        ClickedRowDetails = true;
                    }
                }
                else
                {
                    ClickedRowDetails = true;
                }
            }
            else
            {
                if (((Border)original).DataContext.ToString() != ParentRowDataContext)
                {
                    ClickedRowDetails = true;
                } else if (((Border)original).Child == null)
                {
                    ClickedRowDetails = true;
                }
                else if (!((Border)original).Child.GetType().Equals(typeof(ContentPresenter))) 
                {
                    ClickedRowDetails = true;
                }
            }
        }

        //use if you have scroll viewer surrounding datagrid
        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            object original = e.OriginalSource;

            if (!original.GetType().Equals(typeof(ScrollViewer)))
            {
                if (FindVisualParent<ScrollBar>(original as DependencyObject) != null)
                {
                    ClickedRowDetails = true;
                }
            }
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!OpenedDataGrids.Contains((DataGrid)sender))
                OpenedDataGrids.Add((DataGrid)sender);
        }

        private parentItem FindVisualParent<parentItem>(DependencyObject obj) where parentItem : DependencyObject
        {
            //gets parent object of what we click
            DependencyObject parent = VisualTreeHelper.GetParent(obj);
            while (parent != null && !parent.GetType().Equals(typeof(parentItem)))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as parentItem;
        }
        #endregion Events

        #region Dependency Properties
        public static readonly DependencyProperty RowToColorProperty =
            DependencyProperty.Register("RowToColor", typeof(List<RowColoring>), typeof(CollapsibleSkyWestDataGrid));

        public static readonly DependencyProperty ParentRowDataContextProperty =
           DependencyProperty.Register("ParentRowDataContext", typeof(string), typeof(CollapsibleSkyWestDataGrid));

        public static readonly DependencyProperty ShowColorKeyProperty =
            DependencyProperty.Register("ShowColorKey", typeof(string), typeof(CollapsibleSkyWestDataGrid));

        public static readonly DependencyProperty AutoConvertUTCProperty =
            DependencyProperty.Register("AutoConvertUTC", typeof(bool), typeof(CollapsibleSkyWestDataGrid));

        public static readonly DependencyProperty ObservableCollectionDataTableProperty =
            DependencyProperty.Register("ObservableCollectionDataTable", typeof(DataTable), typeof(CollapsibleSkyWestDataGrid));

        public static readonly DependencyProperty HyperlinkColList =
            DependencyProperty.Register("HyperlinkCols", typeof(List<HyperlinkedColumns>), typeof(CollapsibleSkyWestDataGrid));

        public List<RowColoring> RowToColor
        {
            get { return (List<RowColoring>)GetValue(RowToColorProperty); }
            set { SetValue(RowToColorProperty, value); }
        }

        public string ParentRowDataContext
        {
            get { return (string)GetValue(ParentRowDataContextProperty); }
            set { SetValue(ParentRowDataContextProperty, value); }
        }

        public string ShowColorKey
        {
            get { return (string)GetValue(ShowColorKeyProperty); }
            set { SetValue(ShowColorKeyProperty, value); }
        }

        public bool AutoConvertUTC
        {
            get { return (bool)GetValue(AutoConvertUTCProperty); }
            set { SetValue(AutoConvertUTCProperty, value); }
        }

        public DataTable ObservableCollectionDataTable
        {
            get { return (DataTable)GetValue(ObservableCollectionDataTableProperty); }
            set { SetValue(ObservableCollectionDataTableProperty, value); }
        }

        public List<HyperlinkedColumns> HyperlinkCols
        {
            get { return (List<HyperlinkedColumns>)GetValue(HyperlinkColList); }
            set { SetValue(HyperlinkColList, value); }
        }
        #endregion Dependency Properties

        #region Properties
        public string GroupName { get; set; }

        public bool SkipRecallLayout { get; set; }

        public string SortName { get; set; }

        public bool AutoConvertMilitary { get; set; }

        public ListSortDirection? SortDirection { get; set; }

        public bool ClearFilterOnItemSourceChange { get; set; }

        public bool RememberColumnLayout
        {
            get { return _rememberColumnLayout && CanUserReorderColumns; }
            set { _rememberColumnLayout = value; }
        }
        #endregion Properties

        #region Methods
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public void ShowPassDownMenu()
        {
            //mnuAddWatch.SetBinding( MenuItem.VisibilityProperty, new Binding(Enable));
            mnuAddWatch.Visibility = Visibility.Visible;
            mnuAddWatch.IsEnabled = true;
        }

        public void ShowRemoveTailMenu()
        {
            //mnuAddWatch.SetBinding( MenuItem.VisibilityProperty, new Binding(Enable));
            mnuRemoveTail.Visibility = Visibility.Visible;
            mnuRemoveTail.IsEnabled = true;
        }

        public void HidePassDownMenu()
        {
            //mnuAddWatch.SetBinding( MenuItem.VisibilityProperty, new Binding(Enable));
            mnuAddWatch.Visibility = Visibility.Collapsed;
            mnuAddWatch.IsEnabled = false;
        }

        public DataGridRow GetRow(int row)
        {
            return (DataGridRow)this.ItemContainerGenerator.ContainerFromIndex(row);
        }

        public DataGridCell GetCell(int row, int column)
        {
            DataGridRow dgr = (DataGridRow)this.ItemContainerGenerator.ContainerFromIndex(row);
            System.Windows.Controls.Primitives.DataGridCellsPresenter presenter =
                DataGridHelper.GetVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(dgr);
            return (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
        }

        public void CollapseGrid()
        {
            new SkyWest.Common.MISC().Collapse(this, this.Name);
        }

        #region Menu Events
        private void mnuCopyRow_Click(object sender, RoutedEventArgs e)
        {
            SaveToClipboard();
        }

        private void mnuCopyCell_Click(object sender, RoutedEventArgs e)
        {
            if (this.CurrentCell.Item == DependencyProperty.UnsetValue)
                return;
            var cellInfo = this.CurrentCell;
            if (cellInfo != null)
            {
                var column = cellInfo.Column as DataGridBoundColumn;
                if (column != null)
                {
                    var element = new FrameworkElement() { DataContext = cellInfo.Item };
                    BindingOperations.SetBinding(element, FrameworkElement.TagProperty, column.Binding);
                    string _ColumnValueSelected = element.Tag.ToString();
                    if (_ColumnValueSelected != "")
                        Clipboard.SetDataObject(_ColumnValueSelected, true);
                }
            }

        }

        private void mnuOpenExcel_Click(object sender, RoutedEventArgs e)
        {
            Cursor = Cursors.Wait;
            var moMisc = new SkyWest.Common.MISC();
            if (this.ItemsSource is BindingListCollectionView)
                moMisc.FillExcel((DataView)(this.ItemsSource as BindingListCollectionView).SourceCollection);
            else if (this.ItemsSource is DataView)
                moMisc.FillExcel((DataView)this.ItemsSource);
            else if (this.ItemsSource is DataTable)
                moMisc.FillExcel((DataTable)this.ItemsSource);
            else if (this.ItemsSource is IEnumerable) // for FleetGroupItems only
            {
                DataTable dt2 = DataGridHelper.ToDataTable((IEnumerable<object>)(this.ItemsSource as ListCollectionView).SourceCollection);
                //More generic to allow LOPA
                //var s = (IEnumerable<FleetGroupItems>)((this.ItemsSource as ListCollectionView).SourceCollection);

                //List<FleetGroupItems> fl = s.ToList();
                //var dt = fl.ConvertToDataTable();

                var lstCol = new List<string>();
                foreach (var item in this.Columns)
                {
                    lstCol.Add(item.SortMemberPath.ToString());
                }

                for (int index = dt2.Columns.Count - 1; index >= 0; index--)
                {
                    if (!lstCol.Contains(dt2.Columns[index].ToString()))
                        dt2.Columns.RemoveAt(index);
                }

                moMisc.FillExcel(dt2);
            }
            else
                moMisc.FillExcel(GetItems_ToDataTable(this.ItemsSource));
            Cursor = Cursors.Arrow;
        }

        private DataTable GetItems_ToDataTable(IEnumerable itemsSource)
        {

            DataTable answer = new DataTable();

            if (ObservableCollectionDataTable != null)
                answer = ObservableCollectionDataTable;

            var elements = itemsSource as IList<object> ?? itemsSource.Cast<object>().ToList();
            try
            {
                var item = (elements[0] as IDataTableForExcel);
                if (item != null)
                    return item.GetTable(itemsSource);
            }
            catch (Exception)
            {

            }
            return answer;

            //    if (elements[0].GetType().Name == "PassDownAircraft")
            //    {
            //        var item = (elements[0] as IDataTableForExcel);
            //        if (item != null)
            //            return item.GetTable(itemsSource);
            //    }
            //    if (elements[0].GetType().Name == "VPN")
            //    {
            //        var item = (elements[0] as IVPN);
            //        if (item != null)
            //            return item.GetTable(itemsSource);
            //    }
            //}
            //catch (Exception exception)
            //{

            //}


        }

        private void mnuSaveLayout_Click(object sender, RoutedEventArgs e)
        {
            SaveColumnLayout();
        }

        private void mnuRecallLayout_Click(object sender, RoutedEventArgs e)
        {
            RecallColumnLayout();
        }

        public void SaveColumnLayout()
        {
            var newTemplate = false;
            var repository = new TemplateRepository();
            var temp = repository.GetTemplateByName(Name, TemplateType.Grid, Global.EmpNo);
            if (temp == null)
            {
                temp = new Template(TemplateType.Grid) { Name = Name, OwnerId = Global.EmpNo };
                newTemplate = true;
            }

            var mylist = new List<ColumnsTemplate>();
            foreach (var t in Columns)
            {
                var item = new ColumnsTemplate { DisplayOrder = t.DisplayIndex, SortOrder = t.SortDirection, ColumnWidth = t.Width };
                mylist.Add(item);
            }
            temp.TemplateData = JsonConvert.SerializeObject(mylist, Formatting.Indented);

            string errorMessage = null;
            try
            {
                if (newTemplate)
                    errorMessage = repository.Save(temp);
                else
                    errorMessage = repository.Modify(temp);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            if (!String.IsNullOrWhiteSpace(errorMessage))
                MessageBox.Show(errorMessage, "Error Saving Column Layout", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void RecallColumnLayout()
        {
            var temp = new TemplateRepository().GetTemplateByName(Name, TemplateType.Grid, Global.EmpNo);
            if (temp == null)
                return;
            try
            {
                var columnTemplates = JsonConvert.DeserializeObject<List<ColumnsTemplate>>(temp.TemplateData);
                var columnOrder = new SortedDictionary<int, int>();
                for (var i = 0; i < Columns.Count; i++)
                {
                    var column = Columns[i];
                    var template = columnTemplates[i];
                    columnOrder.Add(template.DisplayOrder, i);
                    column.SortDirection = template.SortOrder;
                    column.Width = template.ColumnWidth;
                }
                foreach (var pair in columnOrder)
                {
                    Columns[pair.Value].DisplayIndex = pair.Key;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException);
            }
        }

        private void mnuWide_Click(object sender, RoutedEventArgs e)
        {
            new SkyWest.Common.MISC().Widen(this, this.Name);
        }

        private void mnuCollapse_Click(object sender, RoutedEventArgs e)
        {
            new SkyWest.Common.MISC().Collapse(this, this.Name);
        }

        private void mnuDefaultWidth_Click(object sender, RoutedEventArgs e)
        {
            SetColumnDefaultWidth();
        }
        #endregion

        private void SaveToClipboard()
        {
            StringBuilder sb = new StringBuilder();

            try
            {

                if (this.SelectedItem is IDataTableForExcel)
                {
                    var item = (IDataTableForExcel)this.SelectedItem;
                    Clipboard.SetDataObject(item.GetDataString(), true);
                }
                else
                {

                    DataRow row = ((DataRowView)this.SelectedItem).Row;

                    foreach (DataColumn c in row.Table.Columns)
                    {
                        sb.Append(c.ColumnName + "\t");
                    }
                    sb.AppendLine();
                    foreach (DataColumn c in row.Table.Columns)
                    {
                        sb.Append(row[c.ColumnName] + "\t");
                    }
                    sb.AppendLine();

                    Clipboard.SetDataObject(sb.ToString(), true);
                }

            }
            catch (Exception)
            {

            }

        }

        public void SetColumnDefaultWidth()
        {
            foreach (DataGridColumn c in this.Columns)
            {
                //c.Width = 150;
            }
        }

        private void DataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Name))
                throw new Exception("Grid name is required.");
            SetColumnDefaultWidth();

            if (!SkipRecallLayout)
                RecallColumnLayout();

            mnuColorKey.IsEnabled = (!string.IsNullOrEmpty(ShowColorKey));
        }

        #region Filter methods
        public List<String> GetFilterList()
        {
            var filterList = new List<String>();
            var filters = GetFilterString();
            if (String.IsNullOrWhiteSpace(filters))
                return filterList;

            foreach (var filter in filters.Split(new[] { " AND " }, StringSplitOptions.None))
            {
                var columnBinding = filter.Substring(filter.IndexOf('[') + 1, filter.IndexOf(']') - 2);
                var columnHeader = GetColumnHeaderByBinding(columnBinding);
                string filterItem;
                if (filter.Contains("# and ["))
                {
                    var startIndex = filter.IndexOf('#') + 1;
                    var count = filter.IndexOf('#', startIndex) - 1 - startIndex;
                    var date = filter.Substring(startIndex, count).ToDateTimeSafe();
                    filterItem = String.Format("{0} = {1:M/d/yyyy}", columnHeader, date);
                }
                else
                    filterItem = filter.Replace(String.Format("[{0}]", columnBinding), columnHeader).TrimStart('(').TrimEnd(')');
                filterList.Add(filterItem);
            }

            return filterList;
        }

        public void RemoveFilter(string filterName)
        {
            var columnBinding = GetColumnBinding(Columns.Single(c => GetColumnHeader(c) == filterName));
            var filter = GetFilterString();
            if (String.IsNullOrWhiteSpace(filter) || !filter.Contains(columnBinding))
                return;
            var startIndex = filter.IndexOf(columnBinding, StringComparison.Ordinal) - 2;
            // Get rid of the preceding " AND " string if it exists
            if (startIndex != 0)
            {
                startIndex -= 5;
                startIndex = startIndex < 0 ? 0 : startIndex;
            }
            var count = filter.Substring(startIndex).IndexOf(")", StringComparison.Ordinal) + 1;
            var newFilter = filter.Remove(startIndex, count);
            newFilter = newFilter.TrimStart(' ', 'A', 'N', 'D', ' ');
            SetFilterString(newFilter);
            TriggerFilterChanged();
        }

        public void SetFilterString(string filter)
        {
            _ignoreFilterChanged = true;
            if (ItemsSource is DataView)
                ((DataView)ItemsSource).RowFilter = filter;
            else if (ItemsSource is DataTable)
                ((DataTable)ItemsSource).DefaultView.RowFilter = filter;
            else if (ItemsSource is BindingListCollectionView)
                ((BindingListCollectionView)ItemsSource).CustomFilter = filter;
            else
                MessageBox.Show("This data grid has not been prepared for filters. Please contact system administrator.",
                    "Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _ignoreFilterChanged = false;
        }

        public string GetFilterString()
        {
            if (ItemsSource is DataView)
                return ((DataView)ItemsSource).RowFilter;
            if (ItemsSource is DataTable)
                return ((DataTable)ItemsSource).DefaultView.RowFilter;
            if (ItemsSource is BindingListCollectionView)
                return ((BindingListCollectionView)ItemsSource).CustomFilter;
            // causing errors when clearing the datagrid in frmMaintenanceRecording grid view. task 3985
            //MessageBox.Show("This data grid has not been prepared for filters. Please contact system administrator.",
            //"Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        public void RemoveAllFilters()
        {
            if (_ignoreFilterChanged)
                return;

            //get the itemssource type
            var _source = ((IEditableCollectionView)ItemsSource);

            if (_source != null)
            {
                var ent = _source.AddNew();
                Type type = ent.GetType();
                _source.CancelNew();

                if (ItemsSource is DataView || ItemsSource is DataTable)
                {
                    DataView source;
                    if (ItemsSource is DataTable)
                        source = ((DataTable)ItemsSource).DefaultView;
                    else
                        source = (DataView)ItemsSource;

                    if (!String.IsNullOrWhiteSpace(source.RowFilter))
                        source.RowFilter = "";
                    mnuRemoveFilter.IsEnabled = false;
                }
                else if (ItemsSource is BindingListCollectionView)
                {
                    var source = (BindingListCollectionView)ItemsSource;
                    if (!String.IsNullOrWhiteSpace(source.CustomFilter))
                        source.CustomFilter = "";
                    mnuRemoveFilter.IsEnabled = false;
                }
                else if (ItemsSource is ICollectionView && type.Name == "FleetGroupItems") // for fleetgroup screen only. Will need to revisit to make this dynamic for all ICollectionViews.
                {
                    var source = (ICollectionView)ItemsSource;
                    source.Filter = item =>
                    {
                        var s = item as FleetGroupItems;
                        if (s.CreatedBy != Global.EmpNo && !s.IsGlobal) return false;
                        return true;
                    };
                    mnuRemoveFilter.IsEnabled = false;
                }
                TriggerFilterChanged();
            }
        }

        private string GetColumnHeaderByBinding(string columnBinding)
        {
            foreach (var column in Columns)
            {
                if (GetColumnBinding(column) == columnBinding)
                {
                    return GetColumnHeader(column);
                }
            }
            return columnBinding;
        }

        private string GetColumnHeader(DataGridColumn column)
        {
            if (column.Header is String)
                return column.Header.ToString();
            var control = (Visual)column.Header;
            while (!(control is TextBlock))
                control = (Visual)VisualTreeHelper.GetChild(control, 0);
            return ((TextBlock)control).Text;
        }

        private void mnuAddFilter_OnClick(object sender, RoutedEventArgs e)
        {
            AddFilter();
        }

        private void mnuRemoveFilter_OnClick(object sender, RoutedEventArgs e)
        {
            RemoveAllFilters();
        }

        private void AddFilter()
        {
            var column = CurrentCell.Column as DataGridBoundColumn;
            if (column == null)
            {
                MessageBox.Show("This column cannot be filtered.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var cellValue = GetCellValue(column);
            if (cellValue == null)
            {
                MessageBox.Show("This column cannot be filtered.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var columnBinding = GetColumnBinding(column);
            if (columnBinding == null)
            {
                MessageBox.Show("This column cannot be filtered.", "Filter Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            ApplyFilters(cellValue, columnBinding);

            TriggerFilterChanged();
        }

        private object GetCellValue(DataGridBoundColumn column)
        {
            var element = new FrameworkElement()
            {
                DataContext = CurrentCell.Item
            };
            BindingOperations.SetBinding(element, TagProperty, column.Binding);
            return element.Tag;
        }

        private string GetColumnBinding(DataGridColumn column)
        {
            try
            {
                return ((Binding)(((DataGridBoundColumn)column).Binding)).Path.Path;
            }
            catch
            {
                return null;
            }
        }

        private void ApplyFilters(object cellValue, string columnBinding)
        {
            _ignoreFilterChanged = true;

            //get the itemssource type
            var source = ((IEditableCollectionView)ItemsSource);
            var ent = source.AddNew();
            Type type = ent.GetType();
            source.CancelNew();

            if (ItemsSource is DataView || ItemsSource is DataTable)
            {
                FilterDataView(cellValue, columnBinding);
            }
            else if (ItemsSource is BindingListCollectionView)
            {
                FilterCollectionView(cellValue, columnBinding);
            }
            else if (ItemsSource is ICollectionView && type.Name == "FleetGroupItems")    // for fleetgroup screen only. Will need to revisit to make this dynamic for all ICollectionViews.
            {
                FilterICollectionView(cellValue, columnBinding);
            }
            else
            {
                MessageBox.Show("This data grid has not been prepared for filters. Please contact system administrator.",
                    "Filter Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _ignoreFilterChanged = false;
        }

        private void FilterICollectionView(object cellValue, string columnBinding)
        {
            var source = (ICollectionView)ItemsSource;

            source.Filter = item =>
            {
                bool include = false;
                var s = item as FleetGroupItems;

                if (s.CreatedBy != Global.EmpNo && !s.IsGlobal) return false;

                Type type = s.GetType();
                PropertyInfo[] properties = type.GetProperties();

                foreach (PropertyInfo property in properties)
                {
                    if (property.Name == columnBinding)
                    {
                        var val = property.GetValue(s);
                        if (Object.Equals(val, cellValue))
                        {
                            include = true;
                            break;
                        }
                    }
                }
                return include;
            };
            mnuRemoveFilter.IsEnabled = true;
        }

        private void FilterDataView(object cellValue, string columnBinding)
        {
            var source = ItemsSource is DataTable
                ? ((DataTable)ItemsSource).DefaultView
                : (DataView)ItemsSource;
            source.RowFilter = GetAllFilters(cellValue, columnBinding, source.RowFilter);
            mnuRemoveFilter.IsEnabled = true;
        }

        private void FilterCollectionView(object cellValue, string columnBinding)
        {
            var source = (BindingListCollectionView)ItemsSource;
            source.CustomFilter = GetAllFilters(cellValue, columnBinding, source.CustomFilter);
            mnuRemoveFilter.IsEnabled = true;
        }

        private string GetAllFilters(object cellValue, string columnBinding, string oldFilter)
        {
            var filter = GetFilter(cellValue, columnBinding);
            if (String.IsNullOrWhiteSpace(oldFilter))
                return filter;
            if (oldFilter.Contains(filter))
                return oldFilter;
            //Keep the "AND" below uppercase for parsing purposes
            return String.Format("{0} AND {1}", oldFilter, filter);
        }

        private string GetFilter(object cellValue, string columnBinding)
        {
            if (cellValue is DateTime)
            {
                var dateFilter = (DateTime)cellValue;
                //Keep the "and" below lowercase for parsing purposes
                return String.Format("([{0}] >= #{1:MM/dd/yyyy H:mm:ss}# and [{0}] <= #{2:MM/dd/yyyy H:mm:ss}#)",
                    columnBinding,
                    dateFilter.Date,
                    dateFilter.Date.AddDays(1).AddSeconds(-1));
            }
            return String.Format("([{0}] = '{1}')", columnBinding, cellValue);
        }

        private void FilterChanged(object sender, EventArgs e)
        {
            RemoveAllFilters();
        }

        private void TriggerFilterChanged()
        {
            var handler = ItemsSourceFilterChanged;
            if (handler != null)
                handler(this, new EventArgs());
        }
        #endregion Filter methods

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (RowToColor != null)
            {
                try
                {
                    DataGridRow dgr = e.Row;
                    DataRowView drv = (DataRowView)dgr.Item;

                    e.Row.Background = defaultBackGroundBrush;
                    e.Row.Foreground = defaultForeGroundBrush;

                    bool bStrikeRow = false;

                    //Go through each of the columns and look for a match
                    for (int i = 0; i < this.Columns.Count; i++)
                    {
                        //For the coloring, go through the RowToColor property
                        for (int j = 0; j < RowToColor.Count; j++)
                        {
                            if (this.Columns[i].Header.ToString() == RowToColor[j].ColumnHeader)
                            {
                                if (drv[i].ToString() == RowToColor[j].ColumnValue)
                                {
                                    e.Row.Background = RowToColor[j].RowBackGroundBrushColor;
                                    e.Row.Foreground = RowToColor[j].RowForeGroundBrushColor;
                                }
                            }
                        }
                    }

                    if (bStrikeRow == true)
                    {
                        DataGridCellsPresenter presenter = DataGridHelper.GetVisualChild<DataGridCellsPresenter>(dgr);
                        if (presenter != null)
                        {
                            for (int i = 0; i < this.Columns.Count; i++)
                            {
                                DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(i);
                                if (cell == null)
                                {
                                    this.ScrollIntoView(dgr, this.Columns[i]);
                                    cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(i);
                                }
                                TextBlock tb = DataGridHelper.GetVisualChild<TextBlock>(cell);
                                tb.TextDecorations = TextDecorations.Strikethrough;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string sNoMessage = ex.Message;
                }
            }
        }

        public static DataGridCell GetCell(DataGrid host, DataGridRow row, int columnIndex)
        {
            if (row == null)
                return null;

            DataGridCellsPresenter presenter = DataGridHelper.GetVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
                return null;

            DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
            if (cell == null)
            {
                host.ScrollIntoView(row, host.Columns[columnIndex]);
                cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
            }
            return cell;
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            try
            {
                if (AutoConvertUTC != null && AutoConvertUTC && e.Column.Header is string && e.Column.Header.ToString().Contains("UTC"))
                {
                    FrameworkElementFactory f = new FrameworkElementFactory(typeof(TextBlock));
                    Binding b;

                    if (AutoConvertMilitary)
                        b = new Binding(e.Column.Header.ToString()) { Converter = new WPF.UtcToLocalConverterMilitary() };
                    else
                        b = new Binding(e.Column.Header.ToString()) { Converter = new WPF.UtcToLocalConverter() };

                    f.SetValue(TextBlock.TextProperty, b);
                    e.Column = new DataGridTemplateColumn() { Header = e.Column.Header.ToString().Replace("UTC", ""), CellTemplate = new DataTemplate() { VisualTree = f }, };
                }
                if (HyperlinkCols != null)
                {
                    for (int j = 0; j < HyperlinkCols.Count; j++)
                    {
                        if (e.Column.Header.ToString() == HyperlinkCols[j].ColumnHeader)
                        {
                            //Create a Cell Template
                            FrameworkElementFactory f = new FrameworkElementFactory(typeof(TextBox));
                            Binding b = new Binding(HyperlinkCols[j].ColumnHeader);
                            b.Mode = BindingMode.OneWay;
                            f.SetValue(TextBox.TextDecorationsProperty, TextDecorations.Underline);
                            f.SetValue(TextBox.TextProperty, b);
                            f.SetValue(TextBox.IsReadOnlyProperty, true);
                            f.SetValue(TextBox.ForegroundProperty, new SolidColorBrush(Colors.Blue));
                            f.SetValue(TextBox.BackgroundProperty, defaultBackGroundBrush);
                            f.SetValue(TextBox.CursorProperty, Cursors.Hand);
                            e.Column = new DataGridTemplateColumn() { Header = e.Column.Header, CellTemplate = new DataTemplate() { VisualTree = f }, };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string sNoMessage = ex.Message;
            }
        }

        private void MnuGroupBy_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            //dynamically pulls in column headers from datagrid and creates a submenu item under Group By
            sourceDataGrid = (DataGrid)sender;
            var rowCount = sourceDataGrid.Columns.Count;
            var mnuItmGroupBy = ((CollapsibleSkyWestDataGrid)(sourceDataGrid)).mnuGroupBy;
            mnuItmGroupBy.Items.Clear();

            for (int i = 0; i < rowCount; i++)
            {
                var columnItem = new MenuItem();
                try
                {
                    columnItem.Header = sourceDataGrid.Columns[i].Header;
                    if (columnItem.Header != null)
                    {
                        var columnName = sourceDataGrid.Columns[i].Header;
                        var menuItem = new MenuItem() { Header = columnName };

                        var dataGridBoundColumn = sourceDataGrid.Columns[i] as DataGridBoundColumn;
                        if (dataGridBoundColumn != null)
                        {
                            //only add the menu item if there is an attached binding to the menu item
                            var binding = dataGridBoundColumn.Binding as Binding;
                            if (binding != null)
                            {
                                var bindingName = binding.Path.Path;
                                menuItem.Name = columnName.ToString().Replace(" ", "").Replace("#", "");
                                menuItem.Click += (x, y) => GroupByMenuItemEventHandler(this, e, bindingName.ToString(CultureInfo.InvariantCulture));
                                mnuItmGroupBy.Items.Add(menuItem);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }

            try
            {
                DependencyObject dep = (DependencyObject)e.OriginalSource;

                // iteratively traverse the visual tree
                while ((dep != null) && !(dep is DataGridCell) && !(dep is DataGridColumnHeader))
                {
                    dep = VisualTreeHelper.GetParent(dep);
                }

                if (dep == null)
                    return;

                if (dep is DataGridColumnHeader)
                {
                    DataGridColumnHeader columnHeader = dep as DataGridColumnHeader;
                    // do something
                }

                if (dep is DataGridCell)
                {
                    DataGridCell cell = dep as DataGridCell;
                    mnuCopyCell.Header = "Copy Cell #" + Convert.ToInt32((cell.Column).DisplayIndex + 1);

                    // navigate further up the tree
                    while ((dep != null) && !(dep is DataGridRow))
                    {
                        dep = VisualTreeHelper.GetParent(dep);
                    }

                    DataGridRow row = dep as DataGridRow;
                    mnuCopyRow.Header = "Copy Row #" + Convert.ToInt32(FindRowIndex(row) + 1);
                }
            }
            catch (Exception ex)
            {
            }
        }

        private int FindRowIndex(DataGridRow row)
        {
            DataGrid dataGrid =
                ItemsControl.ItemsControlFromItemContainer(row)
                as DataGrid;

            int index = dataGrid.ItemContainerGenerator.
                IndexFromContainer(row);

            return index;
        }

        public void GroupByMenuItemEventHandler(object sender, MouseButtonEventArgs e, string fieldName)
        {
            _ignoreFilterChanged = true;
            sourceDataGrid = (DataGrid)sender;
            collectionView = CollectionViewSource.GetDefaultView(sourceDataGrid.ItemsSource);

            if (collectionView.GroupDescriptions.Count > 0)
                collectionView.GroupDescriptions.Clear();

            if (collectionView.SortDescriptions.Count > 0)
                collectionView.SortDescriptions.Clear();

            try
            {
                GroupName = fieldName;
                collectionView.GroupDescriptions.Add(new PropertyGroupDescription(GroupName));
                collectionView.SortDescriptions.Add(new SortDescription(GroupName, ListSortDirection.Ascending));
                CollectionViewSource.GetDefaultView(sourceDataGrid.ItemsSource).Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not group the " + fieldName + " field.\n" + ex.Message, "Unable To Group",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            _ignoreFilterChanged = false;
        }

        public void RestoreGroupAndSort(CollapsibleSkyWestDataGrid swdg)
        {
            _ignoreFilterChanged = true;
            try
            {
                collectionView = CollectionViewSource.GetDefaultView(swdg.ItemsSource);
                if (GroupName != null)
                {
                    if (collectionView.GroupDescriptions.Count > 0)
                        collectionView.GroupDescriptions.Clear();

                    collectionView.GroupDescriptions.Add(new PropertyGroupDescription(GroupName));

                }

                if (SortName != null)
                {
                    if (collectionView.SortDescriptions.Count > 0)
                        collectionView.SortDescriptions.Clear();

                    if (GroupName != null)
                    {
                        collectionView.SortDescriptions.Add(new SortDescription(GroupName, ListSortDirection.Ascending));
                    }

                    collectionView.SortDescriptions.Add(SortDirection.HasValue
                        ? new SortDescription(SortName, SortDirection.Value)
                        : new SortDescription(SortName, ListSortDirection.Ascending));


                }

                CollectionViewSource.GetDefaultView(swdg.ItemsSource).Refresh();
                collectionView.Refresh();
                RecallColumnLayout();
            }
            catch (Exception)
            {
            }
            _ignoreFilterChanged = false;
        }

        private void mnuClearGroupings_Click(object sender, RoutedEventArgs e)
        {
            _ignoreFilterChanged = true;
            try
            {
                if (collectionView.GroupDescriptions.Count > 0)
                    collectionView.GroupDescriptions.Clear();

                if (collectionView.SortDescriptions.Count > 0)
                    collectionView.SortDescriptions.Clear();

                GroupName = null;
                SortName = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to clear Grouping/Sorting: " + ex.Message);
            }
            _ignoreFilterChanged = false;
        }

        private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            _ignoreFilterChanged = true;
            SortName = e.Column.SortMemberPath;
            SortDirection = (e.Column.SortDirection != ListSortDirection.Ascending) ?
                                ListSortDirection.Ascending : ListSortDirection.Descending;
            Dispatcher.BeginInvoke((Action)delegate ()
            {
                _ignoreFilterChanged = false;
            }, null);
        }

        private void DataGrid_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            if (RememberColumnLayout)
                SaveColumnLayout();
        }

        private void DataGridRow_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var handler = RowPreviewMouseDoubleClick;
            if (handler != null)
                handler.Invoke(sender, e);
        }

        private void MnuColorKey_OnClick(object sender, RoutedEventArgs e)
        {
            ColorKey win = new ColorKey(ShowColorKey);
            win.Show();
        }
        #endregion Methods
    }
}
