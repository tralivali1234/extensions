﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace Signum.Web.ScriptCombiner
{
    public class CombineController : Controller
    {
        [AcceptVerbs(HttpVerbs.Get)]
        public void CSS(string f, string p, string v)
        {
            List<IScriptResource> list = new List<IScriptResource>();
            foreach (var local in f.Split(','))
            {
                string path = local;
                if (!string.IsNullOrEmpty(p)) path = p.Replace("%2f", "/") + (p.EndsWith("/") ? "" : "/") + path;
                var r = new CssScriptResource(path);
                if (!string.IsNullOrEmpty(p)) r.resourcesFolder = "../";
                list.Add(r);
            }

            //list.AddRange(f.Split(',').Select(local => new CssScriptResource(local)));

            new MixedCssScriptCombiner(ControllerContext.RequestContext.HttpContext).Process(list);
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public void AreaCss(string f, string v)
        {
            List<IScriptResource> list = new List<IScriptResource>();

            foreach (var area in f.Split(','))
            {
                list.Add(new AreaCssScriptResource(area));
            }

            //list.AddRange(f.Split(',').Select(area => new AreaCssScriptResource(area)));

            new MixedCssScriptCombiner(ControllerContext.RequestContext.HttpContext).Process(list);
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public void CssMixed(string l, string a, string v)
        {
            List<IScriptResource> list = new List<IScriptResource>();

            if (a != null)
                foreach (var area in a.Split(','))
                {
                    list.Add(new AreaCssScriptResource(area));
                }

            if (l != null)
                foreach (var local in l.Split(','))
                {
                    list.Add(new CssScriptResource(local));
                }



            //list.AddRange(l.Split(',').Select(local => new CssScriptResource(local)));
            //list.AddRange(a.Split(',').Select(area => new AreaCssScriptResource(area)));

            new MixedCssScriptCombiner(ControllerContext.RequestContext.HttpContext).Process(list);
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public void JS(string f, string p, string v)
        {
            new JsScriptCombiner().Process(f.Split(','),p,
                ControllerContext.RequestContext.HttpContext);
        }

        [AcceptVerbs(HttpVerbs.Get)]
        public void AreaJs(string f, string v)
        {
            new AreaJsScriptCombiner()
                .Process(f.Split(','), null, ControllerContext.RequestContext.HttpContext);
        }
    }
}
