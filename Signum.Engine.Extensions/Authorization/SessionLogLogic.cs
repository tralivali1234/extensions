﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities.Authorization;
using Signum.Entities;
using Signum.Utilities;
using Signum.Engine.Maps;
using Signum.Engine.DynamicQuery;
using System.Reflection;

namespace Signum.Engine.Authorization
{
    public static class SessionLogLogic
    {
        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                AuthLogic.AssertStarted(sb);
                
                sb.Include<SessionLogDN>();

                PermissionAuthLogic.RegisterPermissions(SessionLogPermission.TrackSession);

                dqm[typeof(SessionLogDN)] = (from sl in Database.Query<SessionLogDN>()
                                             select new
                                             {
                                                 Entity = sl.ToLite(),
                                                 sl.Id,
                                                 sl.User,
                                                 sl.SessionStart,
                                                 sl.SessionEnd,
                                                 sl.SessionTimeOut
                                             }).ToDynamic();
            }
        }

        static bool RoleTracked(Lite<RoleDN> role)
        {
            return SessionLogPermission.TrackSession.IsAuthorized(role);
        }

        public static void SessionStart(string userHostAddress, string userAgent)
        {
            var user = UserDN.Current;
            if (SessionLogLogic.RoleTracked(user.Role.ToLite()))
            {
                using (AuthLogic.Disable())
                {
                    new SessionLogDN
                    {
                        User = user.ToLite(),
                        SessionStart = DateTime.Now.TrimToSeconds(),
                        UserHostAddress = userHostAddress,
                        UserAgent = userAgent
                    }.Save();
                }
            }
        }

        public static void SessionEnd(UserDN user, TimeSpan? timeOut)
        {
            if (user == null || !RoleTracked(user.Role.ToLite()))
                return;

            using (AuthLogic.Disable())
            {
                var log = Database.Query<SessionLogDN>()
                    .Where(sl => sl.User.RefersTo(user))
                    .OrderByDescending(sl => sl.SessionStart)
                    .FirstOrDefault();

                if (log != null && log.SessionEnd == null)
                {
                    log.SessionEnd = timeOut.HasValue ? DateTime.Now.Subtract(timeOut.Value).TrimToSeconds() : DateTime.Now.TrimToSeconds();
                    log.SessionTimeOut = timeOut.HasValue;
                    log.Save();
                }
            }
        }
    }
}