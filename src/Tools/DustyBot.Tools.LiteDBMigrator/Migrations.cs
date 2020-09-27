using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using DustyBot.Framework.LiteDB;
using DustyBot.Framework.LiteDB.Utility;
using DustyBot.Framework.Utility;

namespace DustyBot.Settings.LiteDB
{
    public class Migrations : IMigrations
    {
        private SortedSet<Migration> _migrations = new SortedSet<Migration>();

        public enum Month
        {
            NotSet = 0,
            January = 1,
            February = 2,
            March = 3,
            April = 4,
            May = 5,
            June = 6,
            July = 7,
            August = 8,
            September = 9,
            October = 10,
            November = 11,
            December = 12
        }

        public enum Part
        {
            Whole,
            FirstHalf,
            SecondHalf
        }

        public class Calendar
        {
            public DateTime BeginDate { get; }
            public DateTime EndDate { get; }
            public string Tag { get; }

            public Calendar(DateTime beginDate, DateTime? endDate = null, string tag = null)
            {
                BeginDate = beginDate.Date;
                EndDate = endDate?.Date ?? DateTime.MaxValue.Date;
                Tag = tag;
            }

            public Calendar(int year, Month month, Part part = Part.Whole, string tag = null)
            {
                if (part == Part.Whole)
                {
                    BeginDate = new DateTime(year, (int)month, 1);
                    EndDate = BeginDate.Add(TimeSpan.FromDays(DateTime.DaysInMonth(BeginDate.Year, BeginDate.Month)));
                }
                else if (part == Part.FirstHalf)
                {
                    BeginDate = new DateTime(year, (int)month, 1);
                    EndDate = BeginDate.Add(TimeSpan.FromDays(DateTime.DaysInMonth(BeginDate.Year, BeginDate.Month) / 2));
                }
                else if (part == Part.SecondHalf)
                {
                    BeginDate = new DateTime(year, (int)month, 1).Add(TimeSpan.FromDays(DateTime.DaysInMonth(year, (int)month) / 2));
                    EndDate = new DateTime(year, (int)month, 1).Add(TimeSpan.FromDays(DateTime.DaysInMonth(year, (int)month)));
                }
                else
                    throw new InvalidOperationException();

                Tag = tag;
            }
        }

        public Migrations()
        {
            _migrations = new SortedSet<Migration>()
            {
                new Migration
                (
                    version: 0
                ),

                new Migration
                (
                    version: 1
                ),

                new Migration
                (
                    version: 2,
                    up: db =>
                    {
                        //Change DaumCafeFeeds/LastPostId to signed integer
                        var col = db.GetCollection("MediaSettings");
                        foreach (var settings in col.FindAll())
                        {
                            settings["DaumCafeFeeds"].AsArray?.ForEach(doc =>
                            {
                                doc.AsDocument["LastPostId"] = Convert.ToInt32(doc.AsDocument["LastPostId"].AsInt64);
                            });

                            col.Update(settings);
                        }
                    }
                ),

                new Migration
                (
                    version: 3,
                    up: db =>
                    {
                        var col = db.GetCollection("MediaSettings");
                        foreach (var settings in col.FindAll())
                        {
                            var channelId = settings["ScheduleChannel"].AsUInt64();
                            if (channelId != 0 && settings["ScheduleMessages"].AsArray != null)
                            {
                                var messages = settings["ScheduleMessages"].AsArray;
                                var messageLocs = new List<BsonValue>();
                                foreach (var message in messages)
                                    messageLocs.Add(BsonMapper.Global.ToDocument(new MessageLocation() { MessageId = message.AsUInt64(), ChannelId = channelId }));

                                messages.Clear();
                                messages.AddRange(messageLocs);
                            }

                            settings.Remove("ScheduleChannel");
                            col.Update(settings);
                        }
                    }
                ),

                new Migration
                (
                    version: 4,
                    up: db =>
                    {
                        //Schedule module reworked and settings moved (schedule headers, footers and events will be lost)
                        var mediaCol = db.GetCollection("MediaSettings");
                        var scheduleCol = db.GetCollection("ScheduleSettings");
                        foreach (var mediaSettings in mediaCol.FindAll())
                        {
                            var scheduleSettings = new BsonDocument();
                            scheduleSettings.Add("_id", mediaSettings["_id"]);
                            scheduleSettings.Add("ServerId", mediaSettings["ServerId"]);

                            if (mediaSettings["ScheduleMessages"].AsArray != null)
                            {
                                var scheduleData = new BsonArray();
                                foreach (var mediaMessage in mediaSettings["ScheduleMessages"].AsArray)
                                {
                                    scheduleData.Add(new BsonDocument(new Dictionary<string, BsonValue>()
                                    {
                                        { "MessageId", mediaMessage.AsDocument["MessageId"] },
                                        { "ChannelId", mediaMessage.AsDocument["ChannelId"] }
                                    }));
                                }

                                scheduleSettings.Add("ScheduleData", scheduleData);
                            }

                            scheduleCol.Insert(scheduleSettings);

                            mediaSettings.Remove("ScheduleMessages");
                            mediaCol.Update(mediaSettings);
                        }
                    }
                ),

                new Migration
                (
                    version: 5,
                    up: db =>
                    {
                        //Starboard now allows multiple emojis
                        var col = db.GetCollection("StarboardSettings");
                        foreach (var s in col.FindAll())
                        {
                            if (s["Starboards"].AsArray != null)
                            {
                                foreach (var board in s["Starboards"].AsArray)
                                {
                                    board.AsDocument["Emojis"] = new BsonArray() { board.AsDocument["Emoji"] };
                                    board.AsDocument.Remove("Emoji");
                                }
                            }

                            col.Update(s);
                        }
                    }
                ),

                new Migration
                (
                    version: 6,
                    up: db =>
                    {
                        //Schedule module reworked
                        var map = new Dictionary<ulong, Calendar>
                        {
                        };

                        var col = db.GetCollection("ScheduleSettings");
                        foreach (var s in col.FindAll())
                        {
                            if (s.ContainsKey("Calendars"))
                                continue; //Already migrated

                            var events = new BsonArray();
                            var calendars = new BsonArray();
                            var count = 1;
                            bool showMigrateHelp = false;
                            ulong server = s["ServerId"].AsUInt64();
                            if (s["ScheduleData"].AsArray != null)
                            {
                                foreach (var scheduleMessage in s["ScheduleData"].AsArray.Select(x => x.AsDocument).Where(x => x != null))
                                {
                                    Calendar mapping;
                                    try
                                    {
                                        mapping = map[scheduleMessage["MessageId"].AsUInt64()];
                                        if (mapping == null)
                                            continue;
                                    }
                                    catch (Exception)
                                    {
                                        throw;
                                    }

                                    showMigrateHelp = true;

                                    string title = scheduleMessage["Header"].AsString;
                                    if (string.IsNullOrEmpty(title) || title == "Schedule")
                                        title = null;

                                    calendars.Add(new BsonDocument(new Dictionary<string, BsonValue>()
                                    {
                                        { "MessageId", scheduleMessage["MessageId"] },
                                        { "ChannelId", scheduleMessage["ChannelId"] },
                                        { "Tag", mapping.Tag },
                                        { "BeginDate", mapping.BeginDate },
                                        { "EndDate", mapping.EndDate },
                                        { "Title",  title},
                                        { "Footer", scheduleMessage["Footer"]},
                                    }));

                                    if (scheduleMessage["Events"].AsArray == null)
                                        continue;

                                    foreach (var e in scheduleMessage["Events"].AsArray.Select(x => x.AsDocument).Where(x => x != null))
                                    {
                                        if (e == null)
                                            continue;

                                        events.Add(new BsonDocument(new Dictionary<string, BsonValue>()
                                        {
                                            { "_id", count++ },
                                            { "Tag", mapping.Tag },
                                            { "Date", e["Date"] },
                                            { "HasTime", e["HasTime"] },
                                            { "Description", e["Description"] },
                                        }));
                                    }
                                }
                            }

                            s.Add("NextEventId", count);
                            s.Add("Events", events);
                            s.Add("Calendars", calendars);
                            s.Add("TimezoneOffset", 324000000000); //KST
                            s.Add("ShowMigrateHelp", showMigrateHelp);
                            s.Remove("ScheduleData");
                            col.Update(s);
                        }
                    }
                ),

                new Migration
                (
                    version: 7,
                    up: db =>
                    {
                        var col = db.GetCollection("ScheduleSettings");
                        foreach (var s in col.FindAll())
                        {
                            var calendars = s["Calendars"].AsArray;
                            if (calendars != null)
                            {
                                foreach (var calendar in calendars.Select(x => x.AsDocument).Where(x => x != null))
                                {
                                    calendar.Add("_type", "DustyBot.Settings.RangeScheduleCalendar, DustyBot");
                                }
                            }

                            col.Update(s);
                        }
                    }
                ),

                new Migration
                (
                    version: 8,
                    up: db =>
                    {
                        db.RenameCollection("MiscUserSettings", "LastFmUserSettings");
                    }
                ),

                new Migration
                (
                    version: 9,
                    up: db =>
                    {
                        var col = db.GetCollection("ScheduleSettings");
                        foreach (var s in col.FindAll())
                        {
                            if (TimeSpan.FromTicks(s["TimezoneOffset"].AsInt64) == TimeSpan.FromHours(9))
                                s["TimezoneName"] = "KST";

                            col.Update(s);
                        }
                    }
                ),

                new Migration
                (
                    version: 10,
                    up: db =>
                    {
                        var col = db.GetCollection("ScheduleSettings");
                        foreach (var s in col.FindAll())
                        {
                            if (s["Events"].AsArray != null)
                            {
                                foreach (var e in s["Events"].AsArray)
                                {
                                    var description = e.AsDocument?["Description"]?.AsString;
                                    if (string.IsNullOrEmpty(description))
                                        continue;

                                    var link = DiscordHelpers.TryParseMarkdownUri(description);
                                    if (link.HasValue)
                                    {
                                        e.AsDocument["Description"] = link.Value.Text;
                                        e.AsDocument["Link"] = link.Value.Uri.AbsoluteUri;
                                    }
                                }
                            }

                            col.Update(s);
                        }
                    }
                ),

                new Migration
                (
                    version: 11,
                    up: db =>
                    {
                        var col = db.GetCollection("RolesSettings");
                        foreach (var s in col.FindAll())
                        {
                            if (s["RoleChannel"].AsUInt64() == default &&
                                s["ClearRoleChannel"].AsBoolean == default &&
                                (s["AssignableRoles"].AsArray?.Count ?? 0) == 0 &&
                                (s["AutoAssignRoles"].AsArray?.Count ?? 0) == 0 &&
                                s["PersistentAssignableRoles"].AsBoolean == default &&
                                (s["AdditionalPersistentRoles"].AsArray?.Count ?? 0) == 0 &&
                                (s["PersistentRolesData"].AsArray?.Count ?? 0) == 0)
                            {
                                col.Delete(s["_id"]);
                            }
                        }
                    }
                ),

                new Migration
                (
                    version: 12,
                    up: db =>
                    {
                        var col = db.GetCollection("StarboardSettings");
                        foreach (var s in col.FindAll())
                        {
                            foreach (var starboard in s["Starboards"].AsArray)
                            {
                                foreach (var message in starboard["StarredMessages"].AsDocument)
                                {
                                    var starCount = message.Value["Starrers"].AsArray.Count;
                                    message.Value.AsDocument.Remove("Starrers");
                                    message.Value.AsDocument.Add("StarCount", starCount);
                                }
                            }

                            col.Update(s);
                        }
                    }
                )
            };
        }

        public Migration GetMigration(int version)
        {
            var result = _migrations.ElementAtOrDefault(version);
            if (result == null)
                throw new MigrationException($"Missing migration procedure for version {version}");

            return result;
        }
    }

    #region Legacy types

    public class MessageLocation
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }
    }

    #endregion
}
