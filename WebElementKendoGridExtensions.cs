using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using OpenQA.Selenium;

namespace EndToEndTests
{
    public static class WebElementKendoGridExtensions
    {
        private static string _dataColumnCssSelector = "[ng-bind],[ng-bind-custom]";

        public static void KendoGridNavigateToNextPage(this IWebElement grid, Action onNavigationCompleted = null)
        {
            AssertIsKendoGrid(grid);

            var goToNextPageButton = grid.FindElements(By.CssSelector("a[title='Go to the next page']")).FirstOrDefault();

            if (NotFound(goToNextPageButton))
            {
                throw new InvalidOperationException("Kendo Grid next page button not found");
            }

            goToNextPageButton.Click();

            onNavigationCompleted?.Invoke();
        }

        public static int GetKendoGridCurrentPageNumber(this IWebElement grid)
        {
            AssertIsKendoGrid(grid);

            var selectedPageElement = grid.FindElement(By.CssSelector(".k-pager-numbers .k-state-selected"));

            return int.Parse(selectedPageElement.Text);
        }

        public static void KendoGridNavigateToPreviousPage(this IWebElement grid, Action onNavigationCompleted = null)
        {
            AssertIsKendoGrid(grid);

            var goToPreviousPageButton = grid.FindElements(By.CssSelector("a[title='Go to the previous page']")).FirstOrDefault();

            if (NotFound(goToPreviousPageButton))
            {
                throw new InvalidOperationException("Kendo Grid next page button not found");
            }

            goToPreviousPageButton.Click();

            onNavigationCompleted?.Invoke();
        }


        /// <summary>
        /// Map Kendo Grid data to a data type
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type can be the ViewModel type passed to the .ToDataSourceResult() call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <returns>Mapped data</returns>
       
        public static List<TGridViewModel> GetKendoGridData<TGridViewModel>(this IWebElement grid) where TGridViewModel : new()
        {
            AssertIsKendoGrid(grid);

            return grid
                .GetKendoGridRows()
                .Select(ParseRowData<TGridViewModel>)
                .ToList();
        }

        public static TGridViewModel GetFirstKendoGridData<TGridViewModel>(this IWebElement grid, Expression<Func<TGridViewModel, bool>> condition = null) where TGridViewModel : new()
        {
            AssertIsKendoGrid(grid);

            return grid.GetKendoGridRow(condition).ParseRowData<TGridViewModel>();
        }


        /// <summary>
        /// Check if Kendo Grid has any data satisfying the condition
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type can be the ViewModel type passed to the .ToDataSourceResult() call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <param name="condition">Search expression</param>
        /// <returns>True if any data found based on the condition, False otherwise</returns>
        public static bool KendoGridHasData<TGridViewModel>(this IWebElement grid, Expression<Func<TGridViewModel, bool>> condition) where TGridViewModel : new()
        {
            AssertIsKendoGrid(grid);

            return grid.GetKendoGridData<TGridViewModel>().Any(c => condition.Compile().Invoke(c));
        }

        /// <summary>
        /// Get the Kendo Grid Row which has data that satisfies the condition
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type represents the ViewModel type given to the ToDataSourceResult call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <param name="condition">Search expression</param>
        /// <returns></returns>
        
        public static IWebElement GetKendoGridRow<TGridViewModel>(this IWebElement grid, Expression<Func<TGridViewModel, bool>> condition) where TGridViewModel : new()
        {
            AssertIsKendoGrid(grid);

            return grid
                .GetKendoGridRows()
                .FirstOrDefault(r => condition == null || condition.Compile().Invoke(r.ParseRowData<TGridViewModel>()));
        }

        public static void SetKendoGridInlineEditRowData(this IWebElement row, string property, string value)
        {
            AssertIsKendoGridRow(row);

            var readOnlyControl = row
                .FindElements(By.CssSelector(_dataColumnCssSelector))
                .FirstOrDefault(c=>GetPropertyName(c).Equals(property));

            if (IsValidDataColumn(readOnlyControl))
            {
                var camelCasePropertyName = $"{char.ToLower(property[0])}{property.Substring(1)}";

                readOnlyControl.Click();
                var editControl = row.FindElement(By.CssSelector($"input[name='{camelCasePropertyName}']"));
                editControl.Clear();
                editControl.SendKeys(value);
            }
        }

        private static void AssertIsKendoGridRow(IWebElement control)
        {
            var isKendoGridRow = Browser.WebDriver.ExecuteJavaScript<bool>($"return $(\"[data-uid='{control.GetAttribute("data-uid")}']\").parents('[kendo-grid]').length > 0");
            if (!isKendoGridRow)
            {
                throw new InvalidOperationException("Element is not a kendo grid row");
            }
        }

        private static ReadOnlyCollection<IWebElement> GetKendoGridRows(this IWebElement grid)
        {
            return grid.FindElements(By.CssSelector(".k-grid-content table tr"));
        }

        private static TGridViewModel ParseRowData<TGridViewModel>(this IWebElement gridRow) where TGridViewModel : new()
        {
            var columns = gridRow.FindElements(By.CssSelector("td"));
            var dataItem = new TGridViewModel();
            foreach (var column in columns)
            {
                try
                {
                    var dataColumn = column.FindElements(By.CssSelector(_dataColumnCssSelector)).FirstOrDefault();

                    if (!IsValidDataColumn(dataColumn))
                    {
                        continue;
                    }

                    var propertyName = GetPropertyName(dataColumn);
                    var value = dataColumn.Text;

                    SetProperty(dataItem, propertyName, value);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            return dataItem;
        }

        private static void SetProperty<TGridViewModel>(TGridViewModel dataItem, string propertyName, string value)
        {
            var type = typeof(TGridViewModel);

            if (DateTime.TryParseExact(value, "MM-dd-yyyy hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                type.GetProperty(propertyName)?.SetValue(dataItem, date);
                return;
            }

            type.GetProperty(propertyName)?.SetValue(dataItem, value);
        }

        private static string GetPropertyName(IWebElement dataColumn)
        {
            var attributesToMatch = _dataColumnCssSelector.Split(',').Select(t=>t.Replace("[","").Replace("]",""));
            return attributesToMatch
                .Select(attr => dataColumn.GetAttribute(attr)?.Replace("dataItem.", ""))
                .Select(prop => prop != null ? $"{prop.Capitalize()}" : string.Empty)
                .FirstOrDefault(prop => !string.IsNullOrEmpty(prop));
        }

        private static bool IsValidDataColumn(IWebElement dataColumn)
        {
            return dataColumn != null;
        }

        private static void AssertIsKendoGrid(IWebElement control)
        {
            if (string.IsNullOrEmpty(control.GetAttribute("kendo-grid")))
            {
                throw new InvalidOperationException("Element is not a kendo grid");
            }
        }

        private static bool NotFound(IWebElement element)
        {
            return element == null;
        }
    }
}
