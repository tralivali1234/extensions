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
    
    #line 1 "..\..\Mailing\Views\EmailAttachment.cshtml"
    using Signum.Entities.Mailing;
    
    #line default
    #line hidden
    using Signum.Utilities;
    using Signum.Web;
    
    #line 2 "..\..\Mailing\Views\EmailAttachment.cshtml"
    using Signum.Web.Files;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    [System.Web.WebPages.PageVirtualPathAttribute("~/Mailing/Views/EmailAttachment.cshtml")]
    public partial class _Mailing_Views_EmailAttachment_cshtml : System.Web.Mvc.WebViewPage<dynamic>
    {
        public _Mailing_Views_EmailAttachment_cshtml()
        {
        }
        public override void Execute()
        {
WriteLiteral("\r\n");

            
            #line 4 "..\..\Mailing\Views\EmailAttachment.cshtml"
 using (var sc = Html.TypeContext<EmailAttachmentEmbedded>())
{
    sc.FormGroupStyle = FormGroupStyle.SrOnly;
    
            
            #line default
            #line hidden
            
            #line 7 "..\..\Mailing\Views\EmailAttachment.cshtml"
Write(Html.FileLine(sc, ea => ea.File, fl => { fl.FileType = EmailFileType.Attachment; fl.Remove = false; fl.DragAndDrop = false; }));

            
            #line default
            #line hidden
            
            #line 7 "..\..\Mailing\Views\EmailAttachment.cshtml"
                                                                                                                                   
    
            
            #line default
            #line hidden
            
            #line 8 "..\..\Mailing\Views\EmailAttachment.cshtml"
Write(Html.HiddenLine(sc, c => c.Type));

            
            #line default
            #line hidden
            
            #line 8 "..\..\Mailing\Views\EmailAttachment.cshtml"
                                     
}

            
            #line default
            #line hidden
        }
    }
}
#pragma warning restore 1591
