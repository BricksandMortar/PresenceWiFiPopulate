using Newtonsoft.Json;
using Quartz;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using System;
using System.Linq;

namespace com.bricksandmortarstudio.PresencePopulateAttendance
{

    [DisallowConcurrentExecution]
    [GroupField( "Group", "Group to create Attendance for", true )]
    [LocationField( "Location", "Location to check for presence at", true )]
    [ScheduleField( "Schedule", "Schedule to check for presence matching", true )]
    [InteractionChannelField( "Interaction Channel", "The interaction channel that attendance should be created from", true )]
    [TextField( "Days Back", "The number of days back to look for interactions.", true, "1" )]
    public class PopulateAttendanceFromPresence : IJob
    {
        private const string DONE_KEY = "com_bricksandmortarstudio_Done";

        public void Execute( IJobExecutionContext context )
        {
            var rockContext = new RockContext();
            var jobDataMap = context.JobDetail.JobDataMap;
            var groupGuid = jobDataMap.GetString( "Group" ).AsGuidOrNull();
            var locationGuid = jobDataMap.GetString( "Location" ).AsGuidOrNull();
            var scheduleGuid = jobDataMap.GetString( "Schedule" ).AsGuidOrNull();
            var interactionChannelGuid = jobDataMap.GetString( "InteractionChannel" ).AsGuidOrNull();
            var daysBack = jobDataMap.GetString( "DaysBack" ).AsIntegerOrNull();

            if ( !groupGuid.HasValue || !locationGuid.HasValue || !scheduleGuid.HasValue || !interactionChannelGuid.HasValue || !daysBack.HasValue )
            {
                throw new Exception( "Invalid settings. Ensure the group type, location, schedule, interaction channel, and days back are set." );
            }

            var group = new GroupService( rockContext ).Get( groupGuid.Value );
            var location = new LocationService( rockContext ).Get( locationGuid.Value );
            var schedule = new ScheduleService( rockContext ).Get( scheduleGuid.Value );
            var interactionChannel = new InteractionChannelService( rockContext ).Get( interactionChannelGuid.Value );

            if ( group == null || location == null || schedule == null || interactionChannel == null )
            {
                throw new Exception( "One or more of the settings could not be found. Ensure the group type, location, schedule, and interaction channel are valid." );
            }

            var campusId = location.CampusId;
            if ( !campusId.HasValue )
            {
                throw new Exception( "No campus could be found for " + location.Name );
            }

            var createdCutOff = RockDateTime.Now.AddDays( -1 * daysBack.Value );

            var interactionService = new InteractionService( rockContext );
            var interactions = interactionService.Queryable( "InteractionComponent" )
                .Where( i =>
                    i.InteractionComponent.ChannelId == interactionChannel.Id &&
                    i.InteractionComponent.EntityId == campusId &&
                    i.CreatedDateTime >= createdCutOff &&
                    i.ForeignKey != "com_bricksandmortarstudio_Done"
                )
                .ToList();

            var epochTime = new DateTime( 1970, 1, 1, 0, 0, 0, DateTimeKind.Utc );

            var attendanceService = new AttendanceService( rockContext );
            int counter = 0;
            foreach ( var interaction in interactions )
            {
                var interactionData = JsonConvert.DeserializeObject<InteractionData>( interaction.InteractionData );
                var startTime = epochTime.AddSeconds( interactionData.Arrive );
                var endTime = epochTime.AddSeconds( interactionData.Depart );

                // Gets one day worth of check in times, doing this in the loop is very inefficient
                var checkInTimes = schedule.GetCheckInTimes( startTime );
                if ( interactionData == null )
                {
                    continue;
                }

                // https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods
                if ( !checkInTimes.Any( t => t.CheckInStart < endTime && startTime < t.CheckInEnd ) )
                {
                    continue;
                }

                var attendance = new Attendance();
                attendance.PersonAliasId = interaction.PersonAliasId;
                attendance.ScheduleId = schedule.Id;
                attendance.CampusId = campusId;
                attendance.DidAttend = true;
                attendance.LocationId = location.Id;
                attendance.GroupId = group.Id;
                attendance.StartDateTime = startTime;
                attendanceService.Add( attendance );
                interaction.ForeignKey = DONE_KEY;
                counter++;
            }
            rockContext.SaveChanges();
            context.Result = string.Format( "Added {0} attendance records", counter );
        }
    }

    public partial class InteractionData
    {
        [JsonProperty( "Space" )]
        public string Space { get; set; }

        [JsonProperty( "Arrive" )]
        public long Arrive { get; set; }

        [JsonProperty( "Depart" )]
        public long Depart { get; set; }

        [JsonProperty( "Duration" )]
        public long Duration { get; set; }
    }
}
