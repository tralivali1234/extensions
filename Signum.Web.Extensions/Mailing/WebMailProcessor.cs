﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Signum.Entities.Mailing;
using Signum.Utilities;

namespace Signum.Web.Mailing
{
    public class WebMailOptions
    {
        public bool ForEditing;
        public IEnumerable<EmailAttachmentDN> Attachments;
        public string UntrustedImage;
        public UrlHelper Url;
        public bool HasUntrusted;
    }

    public static class WebMailProcessor
    {
        public static string ReplaceUntrusted(string body, WebMailOptions options)
        {
            if (options.UntrustedImage == null)
                return body; 

            var result = Regex.Replace(body, @"<img [^>]*>", (Match img) =>
            {
                string prevSource = null;

                var newImg = Regex.Replace(img.Value, "src=\"(?<link>[^\"]*)\"", src =>
                {
                    var value = src.Groups["link"].Value;

                    if (!value.StartsWith("cid:"))
                    {
                        options.HasUntrusted = true;
                        prevSource = value;
                        return "src=\"{0}\"".Formato(options.Url.Content(options.UntrustedImage));
                    }

                    return src.Value;
                });

                if (prevSource == null)
                    return newImg;
                
                var title = prevSource;

                var alt = Regex.Match(img.Value, "alt=\"(?<alt>[^\"]*)\"");
                if (alt.Success && alt.Groups["alt"].Value.HasText())
                    title = alt.Groups["alt"].Value + "\r\n" + title;

                return newImg.Insert("<img ".Length, "title=\"" + title + "\" ");
            });

            return result;
        }

        public static string AssertNoUntrusted(string body, WebMailOptions options)
        {
            if (options.UntrustedImage == null)
                return body;

            string imageUrl = options.Url.Content(options.UntrustedImage);

            if (body.Contains(imageUrl))
                throw new InvalidOperationException("{0} found when saving the Email".Formato(imageUrl));

            return body; 
        }

        public static string CidToFilePath(string body, WebMailOptions options)
        {
            if (!options.Attachments.Any())
                return body;
         
            var dic = options.Attachments.Where(a => a.File.FullWebPath.HasText()).ToDictionary(a => a.ContentId, a => options.Url.Content(a.File.FullWebPath));

            var newBody = Regex.Replace(body, "src=\"(?<link>[^\"]*)\"", src =>
            {
                var value = src.Groups["link"].Value;

                if (!value.StartsWith("cid:"))
                {
                    return src.Value;
                }

                value = value.After("cid:");

                var link = dic.TryGetC(value);

                if (link == null)
                    return src.Value;

                return "src=\"{0}\"".Formato(options.Url.Content(link));
            });

            return newBody;
        }

        public static string FilePathToCid(string body, WebMailOptions options)
        {
            if (!options.Attachments.Any())
                return body;

            var dic = options.Attachments.Where(a => a.File.FullWebPath.HasText()).ToDictionary(a => options.Url.Content(a.File.FullWebPath), a => a.ContentId);

            return Regex.Replace(body, "src=\"(?<link>[^\"]*)\"", m =>
            {
                var value = m.Groups["link"].Value;

                var link = dic.TryGetC(value);

                if (link == null)
                    return m.Value;

                return "src=\"cid:{0}\"".Formato(link);
            });
        }
    }
}