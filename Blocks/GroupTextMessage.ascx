<%@ Control Language="C#" AutoEventWireup="true" CodeFile="GroupTextMessage.ascx.cs" Inherits="RockWeb.Plugins.com_plainjoe.GroupText.GroupTextMessage" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title">
                    <i class="fa fa-comments"></i>
                    Group Text Message
                </h1>
            </div>
            <div class="panel-body">

                <Rock:NotificationBox ID="nbWarning" runat="server" NotificationBoxType="Warning" Visible="false" />
                <Rock:NotificationBox ID="nbSuccess" runat="server" NotificationBoxType="Success" Visible="false" />

                <asp:Panel ID="pnlEntry" runat="server">
                    <div class="row">
                        <div class="col-md-6">
                            <Rock:GroupPicker ID="gpGroup" runat="server" Label="Select Group" Required="true"
                                AutoPostBack="true" OnSelectItem="gpGroup_SelectItem"
                                Help="Select the group whose members will receive the text message." />
                        </div>
                    </div>

                    <asp:Panel ID="pnlGroupInfo" runat="server" Visible="false" CssClass="well margin-t-md">
                        <div class="row">
                            <div class="col-md-6">
                                <dl>
                                    <dt>Group Members</dt>
                                    <dd><asp:Literal ID="lMemberCount" runat="server" /></dd>
                                </dl>
                            </div>
                            <div class="col-md-6">
                                <dl>
                                    <dt>Members with Mobile Numbers</dt>
                                    <dd><asp:Literal ID="lMobileCount" runat="server" /></dd>
                                </dl>
                            </div>
                        </div>
                    </asp:Panel>

                    <div class="row margin-t-md">
                        <div class="col-md-8">
                            <Rock:RockTextBox ID="tbMessage" runat="server" Label="Message" TextMode="MultiLine"
                                Rows="4" Required="true" MaxLength="160"
                                Help="Enter the text message to send. Standard SMS messages are limited to 160 characters." />
                            <div class="text-right text-muted small">
                                <span id="charCount">0</span> / 160 characters
                            </div>
                        </div>
                    </div>

                    <div class="actions margin-t-lg">
                        <Rock:BootstrapButton ID="btnSend" runat="server" CssClass="btn btn-primary"
                            Text="Send Message" OnClick="btnSend_Click"
                            DataLoadingText="Sending..." />
                    </div>
                </asp:Panel>

            </div>
        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>

<script type="text/javascript">
    Sys.Application.add_load(function () {
        var messageBox = document.getElementById('<%= tbMessage.ClientID %>');
        var charCount = document.getElementById('charCount');

        if (messageBox && charCount) {
            function updateCount() {
                charCount.textContent = messageBox.value.length;
                if (messageBox.value.length > 160) {
                    charCount.style.color = 'red';
                } else {
                    charCount.style.color = '';
                }
            }

            messageBox.addEventListener('input', updateCount);
            updateCount();
        }
    });
</script>
