using Quartz;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace com.bricksandmortarstudio.PresencePopulateAttendance
{

	[DisallowConcurrentExecution]
	[GroupTypeField("Group Type", "Group Type to Create Attendance For", true)]
	[LocationField("Location", "Location to check for presence at", true)]
	[ScheduleField("Schedule", "Schedule to check for presence matching", true)]
	public class PopulateAttendanceFromPresence : IJob
	{
		public void Execute( IJobExecutionContext context )
		{
			var rockContext = new RockContext();
			var jobDataMap = context.JobDetail.JobDataMap;
			var groupTypeGuid = jobDataMap.GetString( "GroupType" ).AsGuidOrNull();
			var locationGuid = jobDataMap.GetString( "Location" ).AsGuidOrNull();
			var scheduleGuid = jobDataMap.GetString( "Schedule" ).AsGuidOrNull();

			var interactionService = new InteractionService(rockContext);
			var interactions = interactionService.Queryable()
				.AsNoTracking();
		}
	}
}
