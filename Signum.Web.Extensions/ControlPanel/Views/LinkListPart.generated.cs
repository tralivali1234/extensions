﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34011
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Signum.Web.Extensions.ControlPanel.Views
{
    using System;
    using System.Collections.Generic;
    
    #line 1 "..\..\ControlPanel\Views\LinkListPart.cshtml"
    using System.Configuration;
    
    #line default
    #line hidden
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Web;
    using System.Web.Helpers;
    using System.Web.Mvc;
    using System.Web.Mvc.Ajax;
    using System.Web.Mvc.Html;
    using System.Web.Routing;
    using System.Web.Security;
    using System.Web.UI;
    using System.Web.WebPages;
    using Signum.Entities;
    
    #line 2 "..\..\ControlPanel\Views\LinkListPart.cshtml"
    using Signum.Entities.ControlPanel;
    
    #line default
    #line hidden
    
    #line 4 "..\..\ControlPanel\Views\LinkListPart.cshtml"
    using Signum.Entities.DynamicQuery;
    
    #line default
    #line hidden
    
    #line 5 "..\..\ControlPanel\Views\LinkListPart.cshtml"
    using Signum.Entities.Reports;
    
    #line default
    #line hidden
    using Signum.Utilities;
    using Signum.Web;
    
    #line 3 "..\..\ControlPanel\Views\LinkListPart.cshtml"
    using Signum.Web.ControlPanel;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/ControlPanel/Views/LinkListPart.cshtml")]
    public partial class LinkListPart : System.Web.Mvc.WebViewPage<dynamic>
    {
        public LinkListPart()
        {
        }
        public override void Execute()
        {
WriteLiteral("\r\n");

            
            #line 7 "..\..\ControlPanel\Views\LinkListPart.cshtml"
 using (var llp = Html.TypeContext<LinkListPartDN>())
{

            
            #line default
            #line hidden
WriteLiteral("    <ul");

WriteLiteral(" class=\"sf-cp-link-list\"");

WriteLiteral(">\r\n");

            
            #line 10 "..\..\ControlPanel\Views\LinkListPart.cshtml"
        
            
            #line default
            #line hidden
            
            #line 10 "..\..\ControlPanel\Views\LinkListPart.cshtml"
         foreach (LinkElementDN link in Model.Value.Links)
        {

            
            #line default
            #line hidden
WriteLiteral("            <li>\r\n");

WriteLiteral("                ");

            
            #line 13 "..\..\ControlPanel\Views\LinkListPart.cshtml"
           Write(Html.Href(link.Link, link.Label));

            
            #line default
            #line hidden
WriteLiteral("\r\n            </li>\r\n");

            
            #line 15 "..\..\ControlPanel\Views\LinkListPart.cshtml"
        }

            
            #line default
            #line hidden
WriteLiteral("    </ul>\r\n");

            
            #line 17 "..\..\ControlPanel\Views\LinkListPart.cshtml"
}

            
            #line default
            #line hidden
        }
    }
}
#pragma warning restore 1591
