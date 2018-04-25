﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Engine.Maps;
using Signum.Engine.DynamicQuery;
using System.Reflection;
using Signum.Entities.Authorization;
using System.Linq.Expressions;
using Signum.Utilities.DataStructures;
using Signum.Entities;
using Signum.Utilities;
using System.IO;
using Signum.Engine.Operations;
using Signum.Utilities.ExpressionTrees;

namespace Signum.Engine.Authorization
{
    public static class UserTicketLogic
    {
        public static TimeSpan ExpirationInterval = TimeSpan.FromDays(60);
        public static int MaxTicketsPerUser = 4;

        public static bool IsStarted { get; private set; }

        public static void Start(SchemaBuilder sb, DynamicQueryManager dqm)
        {
            if (sb.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                IsStarted = true;

                AuthLogic.AssertStarted(sb);
                sb.Include<UserTicketEntity>()
                    .WithQuery(dqm, () => ut => new
                    {
                        Entity = ut,
                        ut.Id,
                        ut.User,
                        ut.Ticket,
                        ut.ConnectionDate,
                        ut.Device,
                    });

                dqm.RegisterExpression((UserEntity u) => u.UserTickets(), () => typeof(UserTicketEntity).NicePluralName());

                sb.Schema.EntityEvents<UserEntity>().Saving += UserTicketLogic_Saving;
            }
        }

        static void UserTicketLogic_Saving(UserEntity user)
        {
            if (!user.IsNew && user.IsGraphModified & user.InDBEntity(u => u.PasswordHash != user.PasswordHash))
                user.UserTickets().UnsafeDelete();
        }

        static Expression<Func<UserEntity, IQueryable<UserTicketEntity>>> UserTicketsExpression =
            u => Database.Query<UserTicketEntity>().Where(ut => ut.User == u.ToLite());
        [ExpressionField]
        public static IQueryable<UserTicketEntity> UserTickets(this UserEntity u)
        {
            return UserTicketsExpression.Evaluate(u);
        }

        public static string NewTicket(string device)
        {
            using (AuthLogic.Disable())
            using (Transaction tr = new Transaction())
            {
                CleanExpiredTickets(UserEntity.Current);

                UserTicketEntity result = new UserTicketEntity
                {
                    User = UserEntity.Current.ToLite(),
                    Device = device,
                    ConnectionDate = TimeZoneManager.Now,
                    Ticket = Guid.NewGuid().ToString(),
                };

                result.Save();

                return tr.Commit(result.StringTicket());
            }

        }

        public static UserEntity UpdateTicket(string device, ref string ticket)
        {
            using (AuthLogic.Disable())
            using (Transaction tr = new Transaction())
            {
                var pair = UserTicketEntity.ParseTicket(ticket);

                UserEntity user = Database.Retrieve<UserEntity>(pair.userId);
                CleanExpiredTickets(user);

                UserTicketEntity userTicket = user.UserTickets().SingleOrDefaultEx(t => t.Ticket == pair.ticket);
                if (userTicket == null)
                {
                    throw new UnauthorizedAccessException("User attempted to log-in with an invalid ticket");
                }

                UserTicketEntity result = new UserTicketEntity
                {
                    User = user.ToLite(),
                    Device = device,
                    ConnectionDate = TimeZoneManager.Now,
                    Ticket = Guid.NewGuid().ToString(),
                }.Save();

                ticket = result.StringTicket();

                return tr.Commit(user);
            }
        }


        static int CleanExpiredTickets(UserEntity user)
        {
            DateTime min = TimeZoneManager.Now.Subtract(ExpirationInterval);

            int expired = user.UserTickets().Where(d => d.ConnectionDate < min).UnsafeDelete();

            int tooMuch = user.UserTickets().OrderByDescending(t => t.ConnectionDate).Skip(MaxTicketsPerUser).UnsafeDelete();

            return expired + tooMuch;
        }
    }
}
