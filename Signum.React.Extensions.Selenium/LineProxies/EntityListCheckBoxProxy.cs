﻿using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using Signum.Entities;
using Signum.Utilities;

namespace Signum.React.Selenium
{
    public class EntityListCheckBoxProxy : EntityBaseProxy
    {
        public EntityListCheckBoxProxy(IWebElement element, PropertyRoute route)
            : base(element, route)
        {
        }

        public WebElementLocator CheckBoxElement(Lite<Entity> lite)
        {
            return this.Element.WithLocator(By.CssSelector("input[name='{0}']".FormatWith(lite.Key())));
        }

        public List<Lite<Entity>> GetDataElements()
        {
            return this.Element.WithLocator(By.CssSelector("label.sf-checkbox-element")).FindElements().Select(e =>
            {
                var lite = Lite.Parse(e.FindElement(By.CssSelector("input[type=checkbox]")).GetAttribute("name"));
                lite.SetToString(e.FindElement(By.CssSelector("span.sf-entitStrip-link")).Text);
                return lite;
            }).ToList();
        }

        public void SetChecked(Lite<Entity> lite, bool isChecked)
        {
            CheckBoxElement(lite).Find().SetChecked(isChecked);
        }

        public void AssertDataElements(Lite<Entity>[] list, bool orderIndependent = false)
        {
            this.Element.GetDriver().Wait(() =>
            {
                var options = this.GetDataElements();

                if (orderIndependent)
                    return options.OrderBy(a => a.Id).SequenceEqual(list.OrderBy(a => a.Id));
                else
                    return options.SequenceEqual(list);
            });
        }
    }
}
