﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ASP
{
    using System;
    using System.Collections.Generic;
    
    #line 1 "..\..\Dashboard\Views\DashboardView.cshtml"
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
    
    #line 2 "..\..\Dashboard\Views\DashboardView.cshtml"
    using Signum.Entities.Dashboard;
    
    #line default
    #line hidden
    
    #line 4 "..\..\Dashboard\Views\DashboardView.cshtml"
    using Signum.Entities.UserAssets;
    
    #line default
    #line hidden
    using Signum.Utilities;
    using Signum.Web;
    
    #line 3 "..\..\Dashboard\Views\DashboardView.cshtml"
    using Signum.Web.Dashboard;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/Dashboard/Views/DashboardView.cshtml")]
    public partial class _Dashboard_Views_DashboardView_cshtml : System.Web.Mvc.WebViewPage<DashboardEntity>
    {
        public _Dashboard_Views_DashboardView_cshtml()
        {
        }
        public override void Execute()
        {
DefineSection("head", () => {

WriteLiteral("\r\n");

WriteLiteral("    ");

            
            #line 8 "..\..\Dashboard\Views\DashboardView.cshtml"
Write(Html.ScriptCss("~/Dashboard/Content/Dashboard.css"));

            
            #line default
            #line hidden
WriteLiteral("\r\n");

            
            #line 9 "..\..\Dashboard\Views\DashboardView.cshtml"
Write(Html.AntiForgeryToken());

            
            #line default
            #line hidden
WriteLiteral("\r\n");

});

WriteLiteral("\r\n");

            
            #line 12 "..\..\Dashboard\Views\DashboardView.cshtml"
  
    var currentEntity = (Entity)ViewData["currentEntity"];

            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n");

            
            #line 16 "..\..\Dashboard\Views\DashboardView.cshtml"
 using (Model.EntityType == null ? null : CurrentEntityConverter.SetCurrentEntity(currentEntity))
{
    foreach (var gr in Model.Parts.GroupBy(a => a.Row).OrderBy(a => a.Key))
    {
        var lastEnd = 0;

            
            #line default
            #line hidden
WriteLiteral("    <div");

WriteLiteral(" class=\"row row-control-panel\"");

WriteLiteral(">\r\n");

            
            #line 22 "..\..\Dashboard\Views\DashboardView.cshtml"
        
            
            #line default
            #line hidden
            
            #line 22 "..\..\Dashboard\Views\DashboardView.cshtml"
         foreach (var part in gr.OrderBy(a => a.StartColumn))
        {
            var offset = part.StartColumn - lastEnd;

            
            #line default
            #line hidden
WriteLiteral("            <div");

WriteAttribute("class", Tuple.Create(" class=\"", 724), Tuple.Create("\"", 821)
, Tuple.Create(Tuple.Create("", 732), Tuple.Create("part-control-panel", 732), true)
, Tuple.Create(Tuple.Create(" ", 750), Tuple.Create("col-sm-", 751), true)
            
            #line 25 "..\..\Dashboard\Views\DashboardView.cshtml"
, Tuple.Create(Tuple.Create("", 758), Tuple.Create<System.Object, System.Int32>(part.Columns
            
            #line default
            #line hidden
, 758), false)
            
            #line 25 "..\..\Dashboard\Views\DashboardView.cshtml"
, Tuple.Create(Tuple.Create(" ", 771), Tuple.Create<System.Object, System.Int32>(offset == 0 ? null : "col-sm-offset-" + offset
            
            #line default
            #line hidden
, 772), false)
);

WriteLiteral(">\r\n");

WriteLiteral("                ");

            
            #line 26 "..\..\Dashboard\Views\DashboardView.cshtml"
           Write(Html.Partial(DashboardClient.ViewPrefix.FormatWith("PanelPartView"), part));

            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n            </div>\r\n");

            
            #line 29 "..\..\Dashboard\Views\DashboardView.cshtml"
            lastEnd = part.StartColumn + part.Columns;
        }

            
            #line default
            #line hidden
WriteLiteral("    </div>\r\n");

            
            #line 32 "..\..\Dashboard\Views\DashboardView.cshtml"
    }
}

            
            #line default
            #line hidden
WriteLiteral("\r\n");

        }
    }
}
#pragma warning restore 1591
