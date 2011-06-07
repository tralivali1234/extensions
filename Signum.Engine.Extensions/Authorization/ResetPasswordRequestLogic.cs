﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities.Authorization;
using System.Reflection;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Maps;
using Signum.Entities;
using Signum.Engine.Mailing;
using Signum.Engine.Extensions.Properties;
using Signum.Utilities;

namespace Signum.Engine.Authorization
{
    public class ResetPasswordRequestMail : EmailModel<UserDN>
    {
        public string Link;
    }

    public static class ResetPasswordRequestLogic
    {
        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                sb.Include<ResetPasswordRequestDN>();

                dqm[typeof(ResetPasswordRequestDN)] = (from e in Database.Query<ResetPasswordRequestDN>()
                                                       select new
                                                       {
                                                           Entity = e.ToLite(),
                                                           e.Id,
                                                           e.RequestDate,
                                                           e.Code,
                                                           User = e.User.ToLite(),
                                                           e.User.Email
                                                       }).ToDynamic();

                EmailLogic.AssertStarted(sb);

                EmailLogic.RegisterTemplate<ResetPasswordRequestMail>(model =>
                {
                    return new EmailContent
                    {
                        Subject = Resources.ResetPasswordCode,
                        Body = EmailRenderer.Replace(EmailRenderer.ReadFromResourceStream(typeof(AuthLogic).Assembly,
                           "Signum.Engine.Extensions.Authorization.ResetPasswordRequestMail.htm"),
                               model, null, Resources.ResourceManager)
                    };
                });
            }
        }

        public static void ResetPasswordRequest(UserDN user, Func<ResetPasswordRequestDN, string> urlGenerator)
        {
            //Remove old previous requests
            Database.Query<ResetPasswordRequestDN>()
                .Where(r => r.User.Is(user) && r.RequestDate < TimeZoneManager.Now.AddMonths(1))
                .UnsafeDelete();

            ResetPasswordRequestDN rpr = new ResetPasswordRequestDN()
            {
                Code = MyRandom.Current.NextString(5),
                User = user,
                RequestDate = TimeZoneManager.Now,
            }.Save();


            new ResetPasswordRequestMail
            {
                To = user,
                Link = urlGenerator(rpr),
            }.Send();
        }
    }
}