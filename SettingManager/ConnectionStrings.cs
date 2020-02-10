using System;
using System.IO;

namespace M3Logic.Settings
{
    /// <summary>
    /// Creates and holds the set of connection strings for the SettingsManager.
    /// </summary>
    internal class ConnectionStrings
    {
        /// <summary>
        /// SettingsManager default constructor.
        /// </summary>
        /// <param name="domainName">The domain name for the data store, i.e.: AppData/Domain/ApplicationName</param>
        /// <param name="applicationName">The application name for the data store, i.e.: AppData/Domain/ApplicationName</param>
        /// <param name="databaseName">The filename to use for the settings database, i.e. Settings.db</param>
        public ConnectionStrings(string domainName, string applicationName, string databaseName)
        {
            //Throw an exception if either of the parameters are null or empty.
            if (string.IsNullOrEmpty(applicationName) || string.IsNullOrEmpty(domainName) || string.IsNullOrEmpty(databaseName))
            {
                throw new Exception("Domain, Application and Database name are all required to be != IsNullOrEmpty.");
            }
            DomainName = domainName;
            ApplicationName = applicationName;
            DatabaseName = databaseName;

            string specificPath = Path.DirectorySeparatorChar + DomainName +
                Path.DirectorySeparatorChar + ApplicationName +
                Path.DirectorySeparatorChar;

            RoamingUserDbPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + specificPath;
            LocalUserDbPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + specificPath;
            ApplicationDbPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + specificPath;
        }

        /// <summary>
        /// Connection types that the ConnectionStringBuilder can build.
        /// </summary>
        private enum ConnectionType
        {
            LocalUser,
            RoamingUser,
            Application
        }

        //Backing fields
        private string _roamingConnection;
        private string _localConnection;
        private string _applicationConnection;
        //private properties
        private readonly string DomainName;
        private readonly string ApplicationName;

        //These are pretty self-explanatory:
        public readonly string RoamingUserDbPath;
        public readonly string LocalUserDbPath;
        public readonly string ApplicationDbPath;
        public readonly string DatabaseName;

        //These are read-only.
        //Connection string for roaming user settings database.
        public string RoamingConnection
        {
            get
            {
                //Create the connection string the first time it is requested
                if (string.IsNullOrEmpty(_roamingConnection))
                {
                    _roamingConnection = ConnectionStringBuilder(ConnectionType.RoamingUser);
                }
                return _roamingConnection;
            }
        }

        //Connection string for local user settings database.
        public string LocalConnection
        {
            get
            {
                //Create the connection string the first time it is requested
                if (string.IsNullOrEmpty(_localConnection))
                {
                    _localConnection = ConnectionStringBuilder(ConnectionType.LocalUser);
                }
                return _localConnection;
            }
        }

        //Connection string for application settings database.
        public string ApplicationConnection
        {
            get
            {
                //Create the connection string the first time it is requested
                if (string.IsNullOrEmpty(_applicationConnection))
                {
                    _applicationConnection = ConnectionStringBuilder(ConnectionType.Application);
                }
                return _applicationConnection;
            }
        }

        /// <summary>
        /// Builds connections strings based and the passed type.
        /// </summary>
        /// <param name="type">ConnectionType</param>
        /// <returns>Returns a string containing a connection string.</returns>
        private string ConnectionStringBuilder(ConnectionType type)
        {
            string path = "";

            switch (type)
            {
                case ConnectionType.LocalUser:
                    path = LocalUserDbPath;
                    break;
                case ConnectionType.RoamingUser:
                    path = RoamingUserDbPath;
                    break;
                case ConnectionType.Application:
                    path = ApplicationDbPath;
                    break;
            }

            //Add the database name to the end of the path.
            path += this.DatabaseName;

            //Return the fully constructed SQLite connection string.
            return $"Data Source={path};Version=3;";
        }

        /// <summary>
        /// Returns a DatabaseRoute object containing the path and connection string for
        /// a database based on the prefix of the provided key string, if it has one.
        /// If the key has a prefix, it is stripped before returning.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>Returns a DatabaseRoute object.</returns>
        public DatabaseRoute Route(string key)
        {
            DatabaseRoute route = new DatabaseRoute();

            //If no prefix is given, the roaming user store is used as the default.
            //Otherwise, use the appropriate one.
            switch (key.Substring(0, 3))
            {
                case "@ru":
                    route.ConnectionString = this.RoamingConnection;
                    route.Path = this.RoamingUserDbPath;
                    break;
                case "@lu":
                    route.ConnectionString = this.LocalConnection;
                    route.Path = this.LocalUserDbPath;
                    break;
                case "@ap":
                    route.ConnectionString = this.ApplicationConnection;
                    route.Path = this.ApplicationDbPath;
                    break;
                default:
                    route.ConnectionString = this.RoamingConnection;
                    route.Path = this.RoamingUserDbPath;
                    break;
            }
            route.FileName = this.DatabaseName;
            return route;
        }
    }
}