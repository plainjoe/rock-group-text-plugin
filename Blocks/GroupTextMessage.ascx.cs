using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Attribute;
using Rock.Communication;
using Rock.Data;
using Rock.Enums.Group;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_plainjoe.GroupText
{
    [DisplayName( "Group Text Message" )]
    [Category( "Plain Joe > Communications" )]
    [Description( "Allows an admin to send SMS text messages to all members of a selected group." )]
    [IconCssClass( "fa fa-comments" )]

    #region Block Attributes

    [SystemPhoneNumberField(
        "From Number",
        Description = "The system phone number to send the SMS messages from.",
        IsRequired = true,
        Key = AttributeKey.FromNumber,
        Order = 0 )]

    [BooleanField(
        "Include Inactive Members",
        Description = "When enabled, inactive group members will also receive the message.",
        DefaultBooleanValue = false,
        Key = AttributeKey.IncludeInactiveMembers,
        Order = 1 )]

    #endregion

    public partial class GroupTextMessage : RockBlock
    {
        #region Control Declarations

        protected UpdatePanel upnlContent;
        protected Panel pnlView;
        protected NotificationBox nbWarning;
        protected NotificationBox nbSuccess;
        protected Panel pnlEntry;
        protected GroupPicker gpGroup;
        protected Panel pnlGroupInfo;
        protected Literal lMemberCount;
        protected Literal lMobileCount;
        protected RockTextBox tbMessage;
        protected BootstrapButton btnSend;

        #endregion

        #region Attribute Keys

        private static class AttributeKey
        {
            public const string FromNumber = "FromNumber";
            public const string IncludeInactiveMembers = "IncludeInactiveMembers";
        }

        #endregion

        #region Page Parameter Keys

        private static class PageParameterKey
        {
            public const string GroupId = "GroupId";
        }

        #endregion

        #region Base Control Methods

        protected override void OnInit( EventArgs e )
        {
            base.OnInit( e );
            this.BlockUpdated += Block_BlockUpdated;
        }

        protected override void OnLoad( EventArgs e )
        {
            base.OnLoad( e );

            nbWarning.Visible = false;
            nbSuccess.Visible = false;

            if ( !Page.IsPostBack )
            {
                // Check if a GroupId was passed in the page parameters
                var groupId = PageParameter( PageParameterKey.GroupId ).AsIntegerOrNull();
                if ( groupId.HasValue )
                {
                    gpGroup.SetValue( groupId.Value );
                    ShowGroupInfo( groupId.Value );
                }
            }
        }

        #endregion

        #region Events

        protected void Block_BlockUpdated( object sender, EventArgs e )
        {
            // Refresh the block when settings are updated
        }

        protected void gpGroup_SelectItem( object sender, EventArgs e )
        {
            var groupId = gpGroup.SelectedValueAsInt();
            if ( groupId.HasValue )
            {
                ShowGroupInfo( groupId.Value );
            }
            else
            {
                pnlGroupInfo.Visible = false;
            }
        }

        protected void btnSend_Click( object sender, EventArgs e )
        {
            var groupId = gpGroup.SelectedValueAsInt();
            if ( !groupId.HasValue )
            {
                nbWarning.Text = "Please select a group.";
                nbWarning.Visible = true;
                return;
            }

            var message = tbMessage.Text.Trim();
            if ( message.IsNullOrWhiteSpace() )
            {
                nbWarning.Text = "Please enter a message to send.";
                nbWarning.Visible = true;
                return;
            }

            // Get the System Phone Number from block settings
            var fromNumberGuid = GetAttributeValue( AttributeKey.FromNumber ).AsGuidOrNull();
            if ( !fromNumberGuid.HasValue )
            {
                nbWarning.Text = "A 'From Number' has not been configured in the block settings.";
                nbWarning.Visible = true;
                return;
            }

            var rockContext = new RockContext();
            var systemPhoneNumberService = new SystemPhoneNumberService( rockContext );
            var systemPhoneNumber = systemPhoneNumberService.Get( fromNumberGuid.Value );

            if ( systemPhoneNumber == null )
            {
                nbWarning.Text = "The configured 'From Number' could not be found.";
                nbWarning.Visible = true;
                return;
            }

            // Get group members with mobile phone numbers
            var recipients = GetGroupMembersWithMobileNumbers( groupId.Value, rockContext );

            if ( !recipients.Any() )
            {
                nbWarning.Text = "No group members with mobile phone numbers were found.";
                nbWarning.Visible = true;
                return;
            }

            // Create and send the communication
            var result = SendSmsCommunication( recipients, message, systemPhoneNumber, rockContext );

            if ( result.Success )
            {
                nbSuccess.Text = $"Message sent successfully to {result.RecipientCount} recipient(s).";
                nbSuccess.Visible = true;
                tbMessage.Text = string.Empty;
            }
            else
            {
                nbWarning.Text = result.ErrorMessage;
                nbWarning.Visible = true;
            }
        }

        #endregion

        #region Methods

        private void ShowGroupInfo( int groupId )
        {
            var rockContext = new RockContext();
            var groupService = new GroupService( rockContext );
            var group = groupService.Get( groupId );

            if ( group == null )
            {
                pnlGroupInfo.Visible = false;
                return;
            }

            var includeInactive = GetAttributeValue( AttributeKey.IncludeInactiveMembers ).AsBoolean();
            var membersQuery = group.Members.AsQueryable();

            if ( !includeInactive )
            {
                membersQuery = membersQuery.Where( m => m.GroupMemberStatus == GroupMemberStatus.Active );
            }

            var members = membersQuery.ToList();
            var membersWithMobile = GetGroupMembersWithMobileNumbers( groupId, rockContext );

            lMemberCount.Text = members.Count.ToString();
            lMobileCount.Text = membersWithMobile.Count.ToString();

            pnlGroupInfo.Visible = true;
        }

        private List<Person> GetGroupMembersWithMobileNumbers( int groupId, RockContext rockContext )
        {
            var includeInactive = GetAttributeValue( AttributeKey.IncludeInactiveMembers ).AsBoolean();
            var groupMemberService = new GroupMemberService( rockContext );

            var mobilePhoneTypeId = DefinedValueCache.Get( Rock.SystemGuid.DefinedValue.PERSON_PHONE_TYPE_MOBILE ).Id;

            var membersQuery = groupMemberService.Queryable()
                .AsNoTracking()
                .Where( gm => gm.GroupId == groupId );

            if ( !includeInactive )
            {
                membersQuery = membersQuery.Where( gm => gm.GroupMemberStatus == GroupMemberStatus.Active );
            }

            var personsWithMobile = membersQuery
                .Select( gm => gm.Person )
                .Where( p => p.PhoneNumbers.Any( pn =>
                    pn.NumberTypeValueId == mobilePhoneTypeId &&
                    pn.IsMessagingEnabled &&
                    !string.IsNullOrEmpty( pn.Number ) ) )
                .Distinct()
                .ToList();

            return personsWithMobile;
        }

        private SendResult SendSmsCommunication( List<Person> recipients, string message, SystemPhoneNumber fromNumber, RockContext rockContext )
        {
            var result = new SendResult();

            try
            {
                var communicationService = new CommunicationService( rockContext );

                // Create a new communication
                var communication = new Communication
                {
                    Status = CommunicationStatus.Approved,
                    ReviewedDateTime = RockDateTime.Now,
                    ReviewerPersonAliasId = CurrentPersonAliasId,
                    SenderPersonAliasId = CurrentPersonAliasId,
                    CommunicationType = CommunicationType.SMS,
                    IsBulkCommunication = true,
                    FutureSendDateTime = null,
                    Subject = $"Group Text Message - {RockDateTime.Now:g}"
                };

                // Set SMS-specific properties
                communication.SetMediumDataValue( "Message", message );
                communication.SetMediumDataValue( "FromNumber", fromNumber.Id.ToString() );

                communicationService.Add( communication );

                // Add recipients
                foreach ( var person in recipients )
                {
                    var primaryAlias = person.PrimaryAlias;
                    if ( primaryAlias != null )
                    {
                        var recipient = new CommunicationRecipient
                        {
                            PersonAliasId = primaryAlias.Id,
                            Status = CommunicationRecipientStatus.Pending,
                            MediumEntityTypeId = EntityTypeCache.Get( Rock.SystemGuid.EntityType.COMMUNICATION_MEDIUM_SMS ).Id
                        };
                        communication.Recipients.Add( recipient );
                    }
                }

                rockContext.SaveChanges();

                // Send the communication using Rock's message bus
                var sendMessage = new Rock.Tasks.ProcessSendCommunication.Message
                {
                    CommunicationId = communication.Id
                };
                sendMessage.SendAsync();

                result.Success = true;
                result.RecipientCount = communication.Recipients.Count;
            }
            catch ( Exception ex )
            {
                ExceptionLogService.LogException( ex, Context );
                result.Success = false;
                result.ErrorMessage = "An error occurred while sending the message. Please check the exception log for details.";
            }

            return result;
        }

        #endregion

        #region Helper Classes

        private class SendResult
        {
            public bool Success { get; set; }
            public int RecipientCount { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }
}
