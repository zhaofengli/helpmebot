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

namespace helpmebot6
{
    using System.Collections;

    internal class Message
    {
        private readonly DAL _dbal;

        public Message( )
        {
            _dbal = DAL.singleton( );
        }
        
        private ArrayList getMessages(string messageName)
        {
            //"SELECT m.`message_text` FROM message m WHERE m.`message_name` = '"+messageName+"';" );

            DAL.Select q = new DAL.Select("message_text");
            q.setFrom("message");
            q.addWhere(new DAL.WhereConds("message_name", messageName));

            ArrayList resultset = this._dbal.executeSelect(q);

            ArrayList al = new ArrayList();

            foreach (object[] item in resultset)
            {
                al.Add(( item )[0]);
            }
            return al;
        }

        //returns a random message chosen from the list of possible message names
        
        private string chooseRandomMessage(string messageName)
        {
            Random rnd = new Random();
            ArrayList al = getMessages(messageName);
            if (al.Count == 0)
            {
                Helpmebot6.irc.ircPrivmsg(Helpmebot6.debugChannel,
                                          "***ERROR*** Message '" + messageName + "' not found in message table");
                return "";
            }
            return al[rnd.Next(0, al.Count)].ToString();
        }


        private static string buildMessage(string messageFormat, params string[] args)
        {
            return String.Format(messageFormat, args);
        }

        
        public string get(string messageName)
        {
            return chooseRandomMessage(messageName);
        }
        
        public string get(string messageName, params string[] args)
        {

            return buildMessage(chooseRandomMessage(messageName), args);
        }
        
        public void save(string messageName, string messageDescription, string messageContent)
        {
            this._dbal.insert("message", "", messageName, messageDescription, messageContent, "1");
        }
    }
}