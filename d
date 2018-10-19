[1mdiff --git a/src/DustyBot/Modules/ScheduleModule.cs b/src/DustyBot/Modules/ScheduleModule.cs[m
[1mindex c4802c1..47d9bba 100644[m
[1m--- a/src/DustyBot/Modules/ScheduleModule.cs[m
[1m+++ b/src/DustyBot/Modules/ScheduleModule.cs[m
[36m@@ -104,7 +104,11 @@[m [mnamespace DustyBot.Modules[m
         [Parameter("Channel", ParameterType.TextChannel, "target channel")][m
         [Parameter("Header", ParameterType.String, ParameterFlags.Optional, "header for the message")][m
         [Parameter("Footer", ParameterType.String, ParameterFlags.Optional, "footer for the message")][m
[31m-        [Comment("Sends an empty schedule message (e.g. to your `#schedule` channel). You can then add events with the `event add` command.")][m
[32m+[m[32m        [Comment("The `schedule create` command creates an editable message that will contain all of your schedule (past and future events). It can be then edited by your moderators or users that have a special role." +[m
[32m+[m[32m                 "\n\n:one: **You may create more than one message**\nServers usually create one message for each month of schedule or one for every two-weeks etc. You can even maintain multiple different schedules." +[m
[32m+[m[32m                 "\n\n:two: **Where to place the schedule**\nUsually, servers place their schedule messages in a #schedule channel or pin them in main chat or #updates." +[m
[32m+[m[32m                 "\n\n:three: **The \"schedule\" command**\nThe `schedule` command is for convenience. Users can use it to see events happening in the next two weeks across all schedules, with countdowns." +[m
[32m+[m[32m                 "\n\n:four: **Adding events**\nUse the `event add` command to add events. The command adds events to the newest schedule message by default. If you need to add an event to a different message, put its ID as the first parameter.")][m
         public async Task CreateSchedule(ICommand command)[m
         {[m
             await AssertPrivileges(command.Message.Author, command.GuildId);[m
[36m@@ -314,8 +318,8 @@[m [mnamespace DustyBot.Modules[m
             var source = await GetScheduleMessage(command.Guild, (ulong?)command[0]);[m
             var target = await GetScheduleMessage(command.Guild, (ulong?)command[1]);[m
             [m
[31m-            var fromDate = DateTime.ParseExact(command["FromDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
[31m-            var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None) : DateTime.MaxValue;[m
[32m+[m[32m            var fromDate = DateTime.ParseExact(command["FromDate"], new string[] { "yyyy/MM/d", "MM/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
[32m+[m[32m            var toDate = command["ToDate"].HasValue ? DateTime.ParseExact(command["ToDate"], new string[] { "yyyy/MM/d", "MM/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None) : DateTime.MaxValue;[m
 [m
             var moved = source.MoveAll(target, x => x.Date >= fromDate && x.Date < toDate);[m
 [m
[36m@@ -415,7 +419,7 @@[m [mnamespace DustyBot.Modules[m
 [m
             try[m
             {[m
[31m-                var dateTime = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
[32m+[m[32m                var dateTime = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/d", "MM/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
                 bool hasTime = command["Time"].HasValue && command["Time"].AsRegex.Groups[1].Success && command["Time"].AsRegex.Groups[2].Success;[m
                 if (hasTime)[m
                     dateTime = dateTime.Add(new TimeSpan(int.Parse(command["Time"].AsRegex.Groups[1].Value), int.Parse(command["Time"].AsRegex.Groups[2].Value), 0));[m
[36m@@ -470,7 +474,7 @@[m [mnamespace DustyBot.Modules[m
             DateTime? date;[m
             try[m
             { [m
[31m-                date = command["Date"].HasValue ? new DateTime?(DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None)) : null;[m
[32m+[m[32m                date = command["Date"].HasValue ? new DateTime?(DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/d", "MM/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None)) : null;[m
             }[m
             catch (FormatException)[m
             {[m
[36m@@ -530,7 +534,7 @@[m [mnamespace DustyBot.Modules[m
                 var date = e.Date.Date;[m
                 var time = e.Date.TimeOfDay;[m
                 if (command["Date"].HasValue)[m
[31m-                    date = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/dd", "MM/dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
[32m+[m[32m                    date = DateTime.ParseExact(command["Date"], new string[] { "yyyy/MM/d", "MM/d" }, CultureInfo.InvariantCulture, DateTimeStyles.None);[m
 [m
                 bool hasTime = e.HasTime;[m
                 if (command["Time"].HasValue)[m
[1mdiff --git a/src/DustyBot/Modules/SelfModule.cs b/src/DustyBot/Modules/SelfModule.cs[m
[1mindex a177e34..b60571c 100644[m
[1m--- a/src/DustyBot/Modules/SelfModule.cs[m
[1m+++ b/src/DustyBot/Modules/SelfModule.cs[m
[36m@@ -114,12 +114,15 @@[m [mnamespace DustyBot.Modules[m
                     users.Add(user.Id);[m
             }[m
 [m
[32m+[m[32m            var uptime = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime());[m
[32m+[m
             var embed = new EmbedBuilder()[m
                 .WithTitle($"{Client.CurrentUser.Username} (DustyBot v{typeof(Bot).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version})")[m
                 .AddField("Author", "Yebafan#3517", true)[m
                 .AddField("Owners", string.Join("\n", config.OwnerIDs), true)[m
                 .AddField("Presence", $"{users.Count} users\n{guilds.Count} servers", true)[m
                 .AddField("Framework", "v" + typeof(Framework.Framework).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version, true)[m
[32m+[m[32m                .AddField("Uptime", $"{(uptime.Days > 0 ? $"{uptime.Days}d " : "") + (uptime.Hours > 0 ? $"{uptime.Hours}h " : "") + $"{uptime.Minutes}min "}", true)[m
                 .AddField("Web", "http://dustybot.info", true)[m
                 .WithThumbnailUrl(Client.CurrentUser.GetAvatarUrl());[m
 [m
[36m@@ -199,8 +202,8 @@[m [mnamespace DustyBot.Modules[m
             await command.ReplySuccess(Communicator, "Username was changed!").ConfigureAwait(false);[m
         }[m
 [m
[31m-        [Command("dump", "commands", "Generates a list of all commands.", CommandFlags.RunAsync | CommandFlags.OwnerOnly)][m
[31m-        public async Task Commandlist(ICommand command)[m
[32m+[m[32m        [Command("help", "dump", "Generates a list of all commands.", CommandFlags.RunAsync | CommandFlags.OwnerOnly)][m
[32m+[m[32m        public async Task DumpHelp(ICommand command)[m
         {[m
             var config = await Settings.ReadGlobal<BotConfig>();[m
             var result = new StringBuilder();[m
