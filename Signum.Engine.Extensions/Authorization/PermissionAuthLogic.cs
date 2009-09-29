﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Engine.Maps;
using Signum.Entities.Authorization;
using Signum.Entities.Basics;
using Signum.Engine.DynamicQuery;
using Signum.Engine.Basics;
using Signum.Utilities;
using Signum.Utilities.DataStructures;
using System.Threading;
using Signum.Entities;
using System.Reflection;

namespace Signum.Engine.Authorization
{

    public static class PermissionAuthLogic
    {
        static List<Type> permissionTypes = new List<Type>();
        static Dictionary<RoleDN, Dictionary<string, bool>> _runtimeRules;
        static Dictionary<RoleDN, Dictionary<string, bool>> RuntimeRules
        {
            get { return Sync.Initialize(ref _runtimeRules, () => NewCache()); }
        }

        public static void AssertStarted(SchemaBuilder sb)
        {
            sb.AssertDefined(typeof(PermissionAuthLogic).GetMethod("Start")); 
        }

        public static void Start(SchemaBuilder sb)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                AuthLogic.AssertIsStarted(sb);

                sb.Include<RulePermissionDN>();
                sb.Include<PermissionDN>();

                EnumLogic<PermissionDN>.Start(sb, () => permissionTypes.SelectMany(t => Enum.GetValues(t).Cast<Enum>()).ToHashSet());
                sb.Schema.Initializing += Schema_Initializing;
                sb.Schema.Saved += Schema_Saved;
                AuthLogic.RolesModified += UserAndRoleLogic_RolesModified;
            }
        }

        public static void RegisterTypes(params Type[] types)
        {
            permissionTypes.AddRange(types);
        }

        static void Schema_Initializing(Schema sender)
        {
            _runtimeRules = NewCache();
        }

        static void Schema_Saved(Schema sender, IdentifiableEntity ident)
        {
            if (ident is RulePermissionDN)
            {
                Transaction.RealCommit += () => _runtimeRules = null;
            }
        }

        static void UserAndRoleLogic_RolesModified(Schema sender)
        {
            Transaction.RealCommit += () => _runtimeRules = null;
        }

        public static void Authorize(Enum permissionKey)
        {
            if (!GetAllowed(UserDN.Current.Role, EnumDN.UniqueKey(permissionKey)))
                throw new UnauthorizedAccessException("Permission '{0}' is denied".Formato(permissionKey));
        }

        public static bool IsAuthorizedFor(Enum permissionKey)
        {
            return GetAllowed(UserDN.Current.Role, EnumDN.UniqueKey(permissionKey));
        }

        static bool GetAllowed(RoleDN role, string permissionKey)
        {
            return RuntimeRules.TryGetC(role).TryGetS(permissionKey) ?? true;
        }

        static bool GetBaseAllowed(RoleDN role, string permissionKey)
        {
            return role.Roles.Count == 0 ? true :
                  role.Roles.Select(r => GetAllowed(r, permissionKey)).MaxAllowed();
        }

        public static List<AllowedRule> GetAllowedRule(Lazy<RoleDN> roleLazy)
        {
            var role = roleLazy.Retrieve();

            return EnumLogic<PermissionDN>.AllEntities()
                    .Select(p => new AllowedRule(GetBaseAllowed(role, p.Key))
                   {
                       Resource = p,
                       Allowed = GetAllowed(role, p.Key),
                   }).ToList();
        }

        public static void SetAllowedRule(List<AllowedRule> rules, Lazy<RoleDN> roleLazy)
        {
            var role = roleLazy.Retrieve();

            var current = Database.Query<RulePermissionDN>().Where(r => r.Role == role).ToDictionary(a => a.Permission);
            var should = rules.Where(a => a.Overriden).ToDictionary(r => (PermissionDN)r.Resource);

            Synchronizer.Syncronize(current, should,
                (p, pr) => pr.Delete(),
                (p, ar) => new RulePermissionDN { Permission = p, Allowed = ar.Allowed, Role = role }.Save(),
                (p, pr, ar) => { pr.Allowed = ar.Allowed; pr.Save(); });

            _runtimeRules = null;
        }

        public static Dictionary<RoleDN, Dictionary<string, bool>> NewCache()
        {
            using (AuthLogic.Disable())
            using (new EntityCache(true))
            {
                List<RoleDN> roles = AuthLogic.RolesInOrder().ToList();

                Dictionary<RoleDN, Dictionary<string, bool>> realRules = Database.RetrieveAll<RulePermissionDN>()
                    .AgGroupToDictionary(ru => ru.Role, gr => gr.ToDictionary(a => a.Permission.Key, a => a.Allowed));

                Dictionary<RoleDN, Dictionary<string, bool>> newRules = new Dictionary<RoleDN, Dictionary<string, bool>>();
                foreach (var role in roles)
                {
                    var permissions = (role.Roles.Count == 0 ?
                         null :
                         role.Roles.Select(r => newRules.TryGetC(r)).OuterCollapseDictionariesS(vals => vals.MaxAllowed()));

                    permissions = permissions.Override(realRules.TryGetC(role)).Simplify(a => a);

                    if (permissions != null)
                        newRules.Add(role, permissions);
                }

                return newRules;
            }
        }
    }
}
