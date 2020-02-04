using Dapper;
using System;
using System.Collections;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace M3Logic.Settings
{
    /// <summary>
    /// Manages storage and retrieval of settings. Uses naming convention to determine
    /// the storage location of the setting. Requires objects being saved to be
    /// serializable as XML.
    /// 
    /// Prefix  |   Storage Location Example
    /// ------------------------------------------------------------------------
    /// @ru     |   c:\Users\currentuser\AppData\Roaming\Domain\Application Name
    /// @lu     |   c:\Users\currentuser\AppData\Local\Domain\Application Name
    /// @ap     |   c:\ProgramData\Domain\Application Name
    /// none    |   c:\Users\currentuser\AppData\Local\Domain\Application Name
    /// 
    /// This library is designed for ease of use, not raw speed.
    /// 
    /// Inspired by example given here:
    /// https://stackoverflow.com/questions/4120123/c-sharp-application-storing-of-preferences-in-database-or-config-file
    /// Example of use:
    ///     public static bool BooleanFeature
    ///     {
    ///         get { return Settings.GetSetting<bool>("@ruBooleanFeature", true); }
    ///         set { Settings.SaveSetting<bool>("@ruBooleanFeature", value); }
    ///     }
    /// </summary>
    public class SettingManager
    {
        #region Enumerators
        //Defines the behaviors available if a setting is not found.
        public enum SettingNotFoundBehaviors
        {
            ReturnDefault, //Default
            ThrowError
        }
        #endregion

        #region Private fields

        //Object used to lock Hashtable updates.
        private readonly object SyncRoot = new object();

        //Caches settings to prevent excessive database reads.
        private readonly Hashtable _settingsCache = new Hashtable();

        //Stores the connection strings for the different database locations.
        //Instantiated in constructor for this.
        private readonly ConnectionStrings ConnectionStrings;

        #endregion

        #region Public properties
        /// <summary>
        /// Determines whether we throw an error if a setting is missing or just return a default(T).
        /// Default behavior is to return default(T).
        /// </summary>
        public SettingNotFoundBehaviors SettingNotFoundBehavior { get; set; }

        #endregion

        #region Constructor
        /// <summary>
        /// SettingsManager default constructor.
        /// </summary>
        /// <param name="domainName">The domain name for the data store, i.e.: AppData/Domain/ApplicationName</param>
        /// <param name="applicationName">The application name for the data store, i.e.: AppData/Domain/ApplicationName</param>
        public SettingManager(string domainName, string applicationName, string databaseName)
        {
            //Throw an exception if either of the parameters are null or empty.
            if (string.IsNullOrEmpty(domainName) || string.IsNullOrEmpty(applicationName) || string.IsNullOrEmpty(databaseName))
            {
                throw new Exception("Domain, Application and Database name are all required to be != IsNullOrEmpty.");
            }

            //Instantiate ConnectionsStrings, passing along domainName, applicationName and databaseName.
            ConnectionStrings = new ConnectionStrings(domainName, applicationName, databaseName);
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Saves an object to a settings store, determined by the key prefix.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SaveSetting<T>(string key, T value)
        {
            //Add the setting to the hashtable so subsequent reads don't read from the database.
            lock (SyncRoot)
            {
                if (!_settingsCache.ContainsKey(key))
                    _settingsCache[key] = value;
            }

            //Serialize the value object to a XML string.
            string xml = SerializeToString(value);
            //Determine which setting store to use based on key prefix.
            DatabaseRoute route = ConnectionStrings.Route(key);

            //I don't like this double-try setup, it feels kludgey. Must find better solution. #todo
            try //to insert the setting into the database.
            {
                InsertSetting(Strip(key), xml, route);
            }
            catch (SQLiteException e)
            {
                switch ((SQLiteErrorCode)e.ErrorCode)
                {
                    case SQLiteErrorCode.CantOpen: //Database path doesn't exist.
                    case SQLiteErrorCode.Error: //Probably path exists but not database file or file is empty.
                        try //to create the database
                        {
                            CreateDatabase(route);
                        }
                        catch (Exception) //Still getting errors, give up. 🤷‍♂️
                        {
                            throw;
                        }

                        try // again to insert the setting into the database.
                        {
                            InsertSetting(Strip(key), xml, route);
                        }
                        catch (Exception) //Still getting errors, give up. 🤦‍♂️
                        {
                            throw;
                        }

                        break;

                    default: //A SQLite error that we're not prepared for; just pass it along.
                        throw;
                }
            }
            catch (Exception)
            {
                throw; //A non-SQLite error; pass it along.
            }
        }

        /// <summary>
        /// Retrieves a setting associated with a key.
        /// </summary>
        /// <typeparam name="T">The type of object being passed.</typeparam>
        /// <param name="key">The key name used to lookup the setting.</param>
        /// <param name="defaultValue">The default value to return if one is not found.</param>
        /// <returns>Returns an object of type T.</returns>
        public T GetSetting<T>(string key, object defaultValue = null)
        {
            //Is the setting already stored in the hashtable?
            //Return it if it is.
            lock (SyncRoot)
            {
                if (_settingsCache.ContainsKey(key))
                {
                    return (T)_settingsCache[key];
                }
            }

            //Read setting string from database.
            //ConnectionStrings.Route returns a DatabaseRoute object for the relevant
            //setting store database based on the key prefix.
            string retrievedString = ReadSetting(Strip(key), ConnectionStrings.Route(key));

            //If a null empty string is returned, just return the default(T)
            //or the passed-in default if there is one.
            if (string.IsNullOrEmpty(retrievedString))
            {
                if (defaultValue == null)
                {
                    return default;
                }
                else
                {
                    return (T)defaultValue;
                }
            }

            //Create new T object and de-serialize the retrieved XML into it.
            T value = (T)Deserialize<T>(retrievedString);

            //Add the object to the hashtable and return it.
            _settingsCache[key] = value;
            return (T)value;
        }
        #endregion

        #region Data access

        /// <summary>
        /// Creates new settings database at the supplied route.
        /// </summary>
        /// <param name="route">DatabaseRoute to the relevant store.</param>
        private void CreateDatabase(DatabaseRoute route)
        {
            try
            {
                //Create path in the file system.
                FileInfo dir = new FileInfo(route.Path);
                dir.Directory.Create();

                //Create empty database and create Setting table and columns.
                //Merely opening a connection with a connection string will
                //create an empty database in SQLite.
                using (IDbConnection connection = new SQLiteConnection(route.ConnectionString))
                {
                    connection.Execute(@"CREATE TABLE Setting (""Key"" STRING PRIMARY KEY UNIQUE NOT NULL, Value STRING);", new { });
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to create new database file: \"{route.Path + route.FileName}\"", e);
            }
        }

        /// <summary>
        /// Reads a string containing the value of the setting requested from a database.
        /// </summary>
        /// <param name="key">The name of the setting to read.</param>
        /// <param name="route">DatabaseRoute to the relevant store.</param>
        /// <returns>Returns a string.</returns>
        private string ReadSetting(string key, DatabaseRoute route)
        {
            try //to find this setting in the database.
            {
                using (IDbConnection connection = new SQLiteConnection(route.ConnectionString))
                {
                    return (connection.Query<string>(@"SELECT Value FROM Setting WHERE Key = @key;", new { key }).ToList())[0];
                }
            }
            catch (Exception e) //Just pass along any exceptions.
            {
                if (e is ArgumentOutOfRangeException)
                {
                    if (SettingNotFoundBehavior == SettingNotFoundBehaviors.ThrowError)
                    {
                        //Basically this is intended for debugging purposes.
                        //Ideally you should provide a default value when asking for a property instead.
                        throw new ArgumentOutOfRangeException($"Key \"{key}\" not found in database \"{route.Path + route.FileName}\".", e);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                else //Just pass along any exceptions.
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Inserts the setting to the database.
        /// </summary>
        /// <param name="route">DatabaseRoute to the relevant store.</param>
        /// <param name="key">A string containing name of the setting to be stored.</param>
        /// <param name="value">A string containing the XML serialized object to be stored.</param>
        private void InsertSetting(string key, string value, DatabaseRoute route)
        {
            try
            {
                using (IDbConnection connection = new SQLiteConnection(route.ConnectionString))
                {
                    connection.Execute(@"INSERT OR REPLACE INTO Setting (Key,Value) VALUES (@key, @value);",
                        new { key, value });
                }
            }
            catch (Exception) //Just pass along any exceptions.
            {
                throw;
            }
        }
        #endregion

        #region Serialization

        /// <summary>
        /// De-serializes a string of XML into an object T.
        /// </summary>
        /// <typeparam name="T">The type of object to create.</typeparam>
        /// <param name="xml">A XML string.</param>
        /// <returns>Returns an object of type T.</returns>
        private static object Deserialize<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            object result;

            try //to serialize to object of type T.
            {
                using (TextReader reader = new StringReader(xml))
                {
                    result = serializer.Deserialize(reader);
                }
            }
            catch (Exception)
            {
                throw;
            }

            return result;
        }

        /// <summary>
        /// Serializes an object to string of XML.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The object to serialize.</param>
        /// <returns>Returns a string.</returns>
        private static string SerializeToString<T>(T value)
        {
            //If object is empty, just return null.
            if (value == null)
            {
                return string.Empty;
            }

            try //to serialize.
            {
                XmlSerializer serializer = new XmlSerializer(value.GetType());
                StringWriter strWriter = new StringWriter();
                using (XmlWriter xmlWriter = XmlWriter.Create(strWriter))
                {
                    //Serialize to XML
                    serializer.Serialize(xmlWriter, value);
                    //Convert XML to string for easy storage in the database.
                    return strWriter.ToString();
                }
            }
            //No good, show exception details.
            //Object is probably not serializable, such as an anonymous type.
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region Helpers

        /// <summary>
        /// Strips the routing prefix from the key if it has one.
        /// </summary>
        /// <param name="key">Key to be stripped.</param>
        /// <returns>Returns a string without a routing prefix.</returns>
        private string Strip(string key)
        {
            return key.Substring(0, 1) == "@" ? key.Substring(3, key.Length - 3) : key;
        }
        #endregion
    }
}