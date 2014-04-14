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

namespace Signum.Web.Extensions.Profiler.Views
{
    using System;
    using System.Collections.Generic;
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
    using Signum.Utilities;
    
    #line 1 "..\..\Profiler\Views\HeavyList.cshtml"
    using Signum.Utilities.ExpressionTrees;
    
    #line default
    #line hidden
    using Signum.Web;
    
    #line 2 "..\..\Profiler\Views\HeavyList.cshtml"
    using Signum.Web.Profiler;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/Profiler/Views/HeavyList.cshtml")]
    public partial class HeavyList : System.Web.Mvc.WebViewPage<List<HeavyProfilerEntry>>
    {
        public HeavyList()
        {
        }
        public override void Execute()
        {
            
            #line 4 "..\..\Profiler\Views\HeavyList.cshtml"
  
    bool orderByTime = ViewBag.OrderByTime;

            
            #line default
            #line hidden
WriteLiteral("\r\n<h2>");

            
            #line 7 "..\..\Profiler\Views\HeavyList.cshtml"
Write(ViewData[ViewDataKeys.Title]);

            
            #line default
            #line hidden
WriteLiteral("</h2>\r\n<div");

WriteLiteral(" class=\"row\"");

WriteLiteral(">\r\n    <div");

WriteLiteral(" class=\"col-sm-6\"");

WriteLiteral(">\r\n");

WriteLiteral("        ");

            
            #line 10 "..\..\Profiler\Views\HeavyList.cshtml"
   Write(Html.Partial(ProfilerClient.ViewPrefix.Formato("ProfilerButtons")));

            
            #line default
            #line hidden
WriteLiteral("\r\n        <div>\r\n            <br />\r\n");

WriteLiteral("            ");

            
            #line 13 "..\..\Profiler\Views\HeavyList.cshtml"
       Write(Html.ActionLink(orderByTime ? "Order by ID" : "Order by Time", (ProfilerController pc) => pc.Heavy(!orderByTime)));

            
            #line default
            #line hidden
WriteLiteral("\r\n            <br />\r\n");

WriteLiteral("            ");

            
            #line 15 "..\..\Profiler\Views\HeavyList.cshtml"
       Write(Html.ActionLink("Slowest SQLs", (ProfilerController pc) => pc.Statistics(SqlProfileResumeOrder.Sum)));

            
            #line default
            #line hidden
WriteLiteral("\r\n        </div>\r\n    </div>\r\n\r\n    <div");

WriteLiteral(" class=\"col-sm-6\"");

WriteLiteral(" style=\"text-align: right\"");

WriteLiteral(">\r\n");

WriteLiteral("        ");

            
            #line 20 "..\..\Profiler\Views\HeavyList.cshtml"
   Write(Html.ActionLink("Download", (ProfilerController pc) => pc.DownloadFile(null), new { @class = "btn btn-default" }));

            
            #line default
            #line hidden
WriteLiteral("\r\n");

            
            #line 21 "..\..\Profiler\Views\HeavyList.cshtml"
        
            
            #line default
            #line hidden
            
            #line 21 "..\..\Profiler\Views\HeavyList.cshtml"
         using (Html.BeginForm((ProfilerController pc) => pc.UploadFile(), new { enctype = "multipart/form-data", encoding = "multipart/form-data" }))
        {

            
            #line default
            #line hidden
WriteLiteral("            <input");

WriteLiteral(" type=\"file\"");

WriteLiteral(" name=\"xmlFile\"");

WriteLiteral(" style=\"display: inline\"");

WriteLiteral(" />\r\n");

WriteLiteral("            <input");

WriteLiteral(" type=\"submit\"");

WriteLiteral(" value=\"Upload\"");

WriteLiteral(" class=\"btn btn-default\"");

WriteLiteral(" />\r\n");

            
            #line 25 "..\..\Profiler\Views\HeavyList.cshtml"
        }

            
            #line default
            #line hidden
WriteLiteral("    </div>\r\n</div>\r\n\r\n");

            
            #line 29 "..\..\Profiler\Views\HeavyList.cshtml"
 if (Model != null)
{

            
            #line default
            #line hidden
WriteLiteral("    <br />\r\n");

WriteLiteral("    <h3>Entries</h3>\r\n");

WriteLiteral("    <div");

WriteLiteral(" class=\"sf-profiler-chart\"");

WriteLiteral(" data-detail-url=\"");

            
            #line 33 "..\..\Profiler\Views\HeavyList.cshtml"
                                               Write(Url.Action("HeavyRoute", "Profiler"));

            
            #line default
            #line hidden
WriteLiteral("\"");

WriteLiteral(">\r\n    </div>\r\n");

WriteLiteral("    <br />\r\n");

            
            #line 36 "..\..\Profiler\Views\HeavyList.cshtml"
}

            
            #line default
            #line hidden
WriteLiteral("\r\n");

            
            #line 38 "..\..\Profiler\Views\HeavyList.cshtml"
Write(Html.ScriptCss("~/Profiler/Content/Profiler.css"));

            
            #line default
            #line hidden
WriteLiteral("\r\n\r\n<script");

WriteLiteral(" language=\"javascript\"");

WriteLiteral(">\r\n\r\n    $(function () {\r\n");

WriteLiteral("        ");

            
            #line 43 "..\..\Profiler\Views\HeavyList.cshtml"
    Write(new JsFunction(ProfilerClient.Module, "heavyListChart", Model.HeavyDetailsToJson()));

            
            #line default
            #line hidden
WriteLiteral(";\r\n    });\r\n</script>\r\n");

        }
    }
}
#pragma warning restore 1591
