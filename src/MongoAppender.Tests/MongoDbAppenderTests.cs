// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MongoDbAppenderTests.cs" company="Simon Proctor">
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

namespace MongoAppender.Tests
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Threading;
    using System.Xml;

    using log4net;
    using log4net.Config;

    using MongoDB.Bson;
    using MongoDB.Driver;
    using MongoDB.Driver.Builders;

    using NUnit.Framework;

    using SharpTestsEx;

    /// <summary>
    /// Integration test fixture for recording and query for log entries in the test mongo 
    /// database.
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    public class MongoDbAppenderTests
    {
        /// <summary>
        /// The log4net handle
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(MongoDbAppenderTests));

        /// <summary>
        /// The Mongo collection to query for log records
        /// </summary>
        private MongoCollection collection;

        /// <summary>
        /// Initializes static members of the <see cref="MongoDbAppenderTests"/> class. 
        /// Currently this is the log4net setup.
        /// </summary>
        static MongoDbAppenderTests()
        {
            XmlElement element = (XmlElement)ConfigurationManager.GetSection("log4net");
            DOMConfigurator.Configure(element);
        }

        /// <summary>
        /// Sets up access to the default logging collection to query for logs
        /// post any log call.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            MongoClient client = new MongoClient("mongodb://localhost");

            MongoServer conn = client.GetServer();
            MongoDatabase db = conn.GetDatabase("log4net");
            this.collection = db.GetCollection("logs");
        }

        /// <summary>
        /// Tests that the timestamp has been recorded
        /// </summary>
        [Test]
        public void TimestampRecordedTest()
        {
            Log.Info("a log");

            BsonDocument doc = this.collection.FindOneAs<BsonDocument>();
            doc.GetElement("timestamp").Value.Should().Be.OfType<BsonDateTime>();
        }

        /// <summary>
        /// Tests that multiple log statements are accurately logged through the buffer
        /// </summary>
        [Test]
        public void BufferedLogsRecordedTest()
        {
            Log.Info("a log 1");
            Log.Info("a log 2");
            Log.Info("a log 3");
            Log.Info("a log 4");
            Log.Info("a log 5");

            SortByBuilder sbb = new SortByBuilder();
            sbb.Descending("_id");

            var allDocs = this.collection.FindAllAs<BsonDocument>().SetSortOrder(sbb).SetLimit(5);

            BsonDocument doc = allDocs.First();
            Assert.AreEqual(doc.GetElement("message").Value.AsString, "a log 5");
        }

        /// <summary>
        /// Tests that the log level has been recorded
        /// </summary>
        [Test]
        public void LogLevelRecordedTest()
        {
            Log.Info("a log");

            var doc = this.collection.FindOneAs<BsonDocument>();
            doc.GetElement("level").Value.Should().Be.OfType<BsonString>();
            doc.GetElement("level").Value.AsString.Should().Be.EqualTo("INFO");
        }

        /// <summary>
        /// Tests that the log thread has been recorded
        /// </summary>
        [Test]
        public void LogThreadRecordedTest()
        {
            Log.Info("a log");

            BsonDocument doc = this.collection.FindOneAs<BsonDocument>();
            doc.GetElement("thread").Value.Should().Be.OfType<BsonString>();
            doc.GetElement("thread").Value.AsString.Should().Be.EqualTo(Thread.CurrentThread.Name);
        }

        /// <summary>
        /// Tests that the exception has been logged.
        /// </summary>
        [Test]
        public void LogExceptionRecorded()
        {
            try
            {
                throw new ApplicationException("BOOM");
            }
            catch (Exception e)
            {
                Log.Fatal("a log", e);
            }

            SortByBuilder sbb = new SortByBuilder();
            sbb.Descending("_id");

            var allDocs = this.collection.FindAllAs<BsonDocument>().SetSortOrder(sbb).SetLimit(1);
            
            BsonDocument doc = allDocs.First();
            doc.GetElement("exception").Value.Should().Be.OfType<BsonString>();
            doc.GetElement("exception").Value.AsString.Should().Contain("BOOM");
        }
    }
}
