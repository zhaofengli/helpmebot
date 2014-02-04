﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CoreConfiguration.cs" company="Helpmebot Development Team">
//   Helpmebot is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//   
//   Helpmebot is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//   
//   You should have received a copy of the GNU General Public License
//   along with Helpmebot.  If not, see http://www.gnu.org/licenses/ .
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace Helpmebot.Configuration.XmlSections
{
    using System.Configuration;

    using Helpmebot.Configuration.XmlSections.Interfaces;

    /// <summary>
    ///     The database configuration.
    /// </summary>
    public class CoreConfiguration : ConfigurationSection, ICoreConfiguration
    {
        #region Public Properties

        /// <summary>
        ///     Gets the debug channel.
        /// </summary>
        [ConfigurationProperty("debugChannel", IsRequired = true, DefaultValue = "##helpmebot")]
        public string DebugChannel
        {
            get
            {
                return (string)base["debugChannel"];
            }
        }

        /// <summary>
        ///     Gets the HTTP timeout
        /// </summary>
        [ConfigurationProperty("httpTimeout", IsRequired = true, DefaultValue = 5000)]
        public int HttpTimeout
        {
            get
            {
                return (int)base["httpTimeout"];
            }
        }

        /// <summary>
        ///     Gets the user agent.
        /// </summary>
        [ConfigurationProperty(
            "useragent", 
            IsRequired = true, 
            DefaultValue = "Helpmebot/6.0 (+http://helpmebot.org.uk)")]
        public string UserAgent
        {
            get
            {
                return (string)base["useragent"];
            }
        }

        /// <summary>
        ///     Gets a value indicating whether to authenticate to services
        /// </summary>
        [ConfigurationProperty(
            "authToServices",
            IsRequired = true,
            DefaultValue = true)]
        public bool AuthToServices
        {
            get
            {
                return (bool)base["authToServices"];
            }
        }

        #endregion
    }
}