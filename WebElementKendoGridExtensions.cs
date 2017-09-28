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
        /// <summary>
        /// Map Kendo Grid data to a data type
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type can be the ViewModel type passed to the .ToDataSourceResult() call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <returns>Mapped data</returns>
        public static List<TGridViewModelType> GetKendoGridData<TGridViewModelType>(this IWebElement control) where TGridViewModelType : new()
        {
            AssertIsKendoGrid(control);

            return control
                .GetKendoGridRows()
                .Select(ParseRowData<TGridViewModelType>)
                .ToList();
        }

        /// <summary>
        /// Check if Kendo Grid has any data satisfying the condition
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type can be the ViewModel type passed to the .ToDataSourceResult() call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <param name="condition">Search expression</param>
        /// <returns>True if any data found based on the condition, False otherwise</returns>
        public static bool KendoGridHasData<TGridViewModelType>(this IWebElement control, Expression<Func<TGridViewModelType,bool>> condition) where TGridViewModelType : new()
        {
            AssertIsKendoGrid(control);

            return control.GetKendoGridData<TGridViewModelType>().Any(c => condition.Compile().Invoke(c));
        }

        
        /// <summary>
        /// Get the Kendo Grid Row which has data that satisfies the condition
        /// </summary>
        /// <typeparam name="TGridViewModelType">Target type for mapping. This type represents the ViewModel type given to the ToDataSourceResult call or a type matching the GridOptions.Column names</typeparam>
        /// <param name="control">KendoGrid WebElement</param>
        /// <param name="condition">Search expression</param>
        /// <returns></returns>
        public static IWebElement GetKendoGridRow<TGridViewModelType>(this IWebElement control, Expression<Func<TGridViewModelType, bool>> condition) where TGridViewModelType : new()
        {
            AssertIsKendoGrid(control);

            return control
                .GetKendoGridRows()
                .FirstOrDefault(r => condition.Compile().Invoke(r.ParseRowData<TGridViewModelType>()));
        }

        private static ReadOnlyCollection<IWebElement> GetKendoGridRows(this IWebElement grid)
        {
            return grid.FindElements(By.CssSelector(".k-grid-content table tr"));
        }

        private static TGridViewModelType ParseRowData<TGridViewModelType>(this IWebElement gridRow) where TGridViewModelType : new()
        {
            var type = typeof(TGridViewModelType);
            var columns = gridRow.FindElements(By.CssSelector("td"));
            var dataItem = new TGridViewModelType();
            foreach (var column in columns)
            {
                try
                {
                    var dataColumn = column.FindElement(By.CssSelector("[ng-bind]"));
                    var propertyName = dataColumn.GetAttribute("ng-bind").Replace("dataItem.", "");
                    var camelCasePropertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                    var value = dataColumn.Text;
                    type.GetProperty(camelCasePropertyName).SetValue(dataItem, value);
                }
                catch (NoSuchElementException)
                {
                    // suppress exception if column is the "actions" column
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
            }

            return dataItem;
        }

        private static void AssertIsKendoGrid(IWebElement control)
        {
            if (string.IsNullOrEmpty(control.GetAttribute("kendo-grid")))
            {
                throw new InvalidOperationException("Element is not a kendo grid");
            }
        }
    }
}
