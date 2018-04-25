﻿using OpenQA.Selenium;
using Signum.Entities;

namespace Signum.React.Selenium
{
    public class FileLineProxy : BaseLineProxy
    {
        public FileLineProxy(IWebElement element, PropertyRoute route)
            : base(element, route)
        {
            
        }

        public void SetPath(string path)
        {
            FileElement.Find().SendKeys(path);
            FileElement.WaitNoPresent();
        }

        private WebElementLocator FileElement
        {
            get { return this.Element.WithLocator(By.CssSelector("input[type=file]")); }
        }
    }

    public static class FileExtensions
    {
        public static LineContainer<T> SetPath<T>(this EntityRepeaterProxy repeater, string path) where T : ModifiableEntity
        {
            var count = repeater.ItemsCount();

            var input = repeater.Element.FindElement(By.CssSelector("input[type=file]"));
            input.SendKeys(path);

            return repeater.Details<T>(count + 1);
        }
    }
}
