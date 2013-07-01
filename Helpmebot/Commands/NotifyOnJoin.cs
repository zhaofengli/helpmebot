﻿// /****************************************************************************
//  *   This file is part of Helpmebot.                                        *
//  *                                                                          *
//  *   Helpmebot is free software: you can redistribute it and/or modify      *
//  *   it under the terms of the GNU General Public License as published by   *
//  *   the Free Software Foundation, either version 3 of the License, or      *
//  *   (at your option) any later version.                                    *
//  *                                                                          *
//  *   Helpmebot is distributed in the hope that it will be useful,           *
//  *   but WITHOUT ANY WARRANTY; without even the implied warranty of         *
//  *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the          *
//  *   GNU General Public License for more details.                           *
//  *                                                                          *
//  *   You should have received a copy of the GNU General Public License      *
//  *   along with Helpmebot.  If not, see <http://www.gnu.org/licenses/>.     *
//  ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using helpmebot6;
using helpmebot6.Commands;

namespace helpmebot6.Commands
{
    internal class Notifyonjoin : GenericCommand
    {
        private static object dictlock = new object();
        private static Dictionary<string, List<User>> RequestedNotifications = new Dictionary<string, List<User>>();

        protected override CommandResponseHandler execute(User source, string channel, string[] args)
        {
            if (args.Length != 1) return new CommandResponseHandler("I actually expected a nickname as single argument");
            string trigger;
            lock (dictlock)
            {
                User toNotify = source;
                trigger = args[0];
                if (!RequestedNotifications.ContainsKey(trigger)) RequestedNotifications.Add(trigger,new List<User>());
                RequestedNotifications[trigger].Add(toNotify);
            }
            return new CommandResponseHandler(String.Format("I'll send you a private message when someone with nickname {0} joins a channel I'm in", trigger));
        }

        internal void notifyJoin(User source, string channel)
        {
            List<User> toNotify = null;
            lock (dictlock)
            {
                if (RequestedNotifications.TryGetValue(source.nickname, out toNotify))
                {
                    RequestedNotifications.Remove(source.nickname);
                }
            }
            if (toNotify != null)
            {
                string message = String.Format("I just saw {0} enter channel {1}", source.nickname, channel);
                foreach (User user in toNotify)
                {
                    Helpmebot6.irc.ircPrivmsg(user.nickname, message);
                }
            }
        }
    }
}
