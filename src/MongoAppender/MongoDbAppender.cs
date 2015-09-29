// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MongoDbAppender.cs" company="Simon Proctor">
//   Copyright (c) 2014 Simon Proctor
//   
//   Permission is hereby granted, free of charge, to any person obtaining a copy
//   of this software and associated documentation files (the "Software"), to deal
//   in the Software without restriction, including without limitation the rights
//   to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//   copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions:
//   
//   The above copyright notice and this permission notice shall be included in
//   all copies or substantial portions of the Software.
//   
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//   AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//   LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//   OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//   THE SOFTWARE.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace MongoAppender
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;

    using log4net.Appender;
    using log4net.Core;

    using MongoDB.Bson;
    using MongoDB.Driver;

    /// <summary>
    /// The log4net appender. Saves log records to a mongo database.
    /// </summary>
    public class MongoDbAppender : BufferingAppenderSkeleton
	{
        /// <summary>
        /// The list of log fields to save. Initialised from the log4net configuration
        /// </summary>
		private readonly List<MongoAppenderField> fields = new List<MongoAppenderField>();

        /// <summary>
        /// Gets or sets the MongoDB database connection in the format:
        /// mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]
        /// If no database specified, default to "log4net"
        /// </summary>
        public string ConnectionString { get; set; }

		/// <summary>
		/// Gets or sets the connectionString name to use in the connectionStrings section of the *.config file
		/// If not specified or connectionString name does not exist will use ConnectionString value
		/// </summary>
		public string ConnectionStringName { get; set; }

		/// <summary>
		/// Gets or sets the name of the collection in the database. Defaults to "logs"
		/// </summary>
		public string CollectionName { get; set; }

        /// <summary>
        /// Gets or sets Mongo collection to write to. Initialised when the appender is activated
        /// </summary>
        private MongoCollection Collection { get; set; }

        /// <summary>
        /// Adds an entry from the config to the list of fields to log
        /// </summary>
        /// <param name="field">The field to log</param>
		public void AddField(MongoAppenderField field)
		{
			fields.Add(field);
		}

        /// <summary>
        /// Initialize the appender based on the options set
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is part of the <see cref="IOptionHandler"/> delayed object
        /// activation scheme. The <see cref="ActivateOptions"/> method must 
        /// be called on this object after the configuration properties have
        /// been set. Until <see cref="ActivateOptions"/> is called this
        /// object is in an undefined state and must not be used. 
        /// </para>
        /// <para>
        /// If any of the configuration properties are modified then 
        /// <see cref="ActivateOptions"/> must be called again.
        /// </para>
        /// </remarks>
        public override void ActivateOptions()
        {
            base.ActivateOptions();

            Collection = GetCollection();
        }

        /// <summary>
        /// Appends a logging event to Mongo
        /// </summary>
        /// <param name="loggingEvent">The logging event</param>
		protected override void Append(LoggingEvent loggingEvent)
        {
            BsonDocument record = BuildBsonDocument(loggingEvent);
            Collection.Insert(record);
        }

        /// <summary>
        /// Inserts the events into the database.
        /// </summary>
        /// <param name="events">The events to insert into the database.</param>
        /// <remarks>
        /// <para>
        /// Insert all the events specified in the <paramref name="events"/>
        /// array into the database.
        /// </para>
        /// </remarks>
        protected override void SendBuffer(LoggingEvent[] events)
        {
            foreach (LoggingEvent logEvent in events)
            {
                Append(logEvent);
            }
        }

        /// <summary>
        /// Gets the Mongo collection that the logs will be written to. If one isn't specified 
        /// in the configuration then it defaults to 'logs'.
        /// </summary>
        /// <returns>The Mongo collection</returns>
		protected virtual MongoCollection GetCollection()
		{
			MongoDatabase db = GetDatabase();
			MongoCollection collection = db.GetCollection(CollectionName ?? "logs");
			return collection;
		}

        /// <summary>
        /// Gets the connection string by name or by using the connection string property if unavailable.
        /// </summary>
        /// <returns>The connection string</returns>
		protected virtual string GetConnectionString()
		{
			ConnectionStringSettings connectionStringSetting = ConfigurationManager.ConnectionStrings[ConnectionStringName];
			return connectionStringSetting != null ? connectionStringSetting.ConnectionString : ConnectionString;
		}

        /// <summary>
        /// Gets the Mongo database based on the connection string. IF the database name isn't 
        /// present in the connection string it defaults to 'log4net'.
        /// </summary>
        /// <returns>The Mongo database</returns>
		protected virtual MongoDatabase GetDatabase()
		{
            string connectionString = GetConnectionString();

			MongoUrl url = MongoUrl.Create(connectionString);
            MongoClient client = new MongoClient(url);
			MongoServer conn = client.GetServer();

			MongoDatabase db = conn.GetDatabase(url.DatabaseName ?? "log4net");
			return db;
		}

        /// <summary>
        /// Builds the BSON document to send to Mongo from the log4net LoggingEvent.
        /// </summary>
        /// <param name="log">The logging event</param>
        /// <returns>The BSON document</returns>
		private BsonDocument BuildBsonDocument(LoggingEvent log)
		{
            BsonDocument doc = new BsonDocument();

            foreach (MongoAppenderField field in fields)
            {
                try
                {
                    if (field.Layout != null)
                    {
                        object value = field.Layout.Format(log);
                        BsonValue bsonValue = value as BsonValue ?? BsonValue.Create(value);
                        doc.Add(field.Name, bsonValue);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }

		    return doc;
		}
	}
}