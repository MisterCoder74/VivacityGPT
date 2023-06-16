Imports System.Net
Imports System.IO
Imports System.Configuration
Imports System.Security.Cryptography
Imports System.Speech.Synthesis
Imports System.Speech.Recognition
Imports System.Net.SecurityProtocolType


Public Class frmGPTChat
    Dim sanswer As String
    Dim typeanswer As String
    Dim OPENAI_API_KEY As String = "" 'https://beta.openai.com/account/api-keys
    Dim oSpeechRecognitionEngine As SpeechRecognitionEngine = Nothing
    Dim oSpeechSynthesizer As System.Speech.Synthesis.SpeechSynthesizer = Nothing

    Private Sub frmGPTChat_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Dim oAppSettingsReader As New AppSettingsReader()
        Dim sApiKey As String = oAppSettingsReader.GetValue("OPENAI_API_KEY", GetType(String)) & ""

        If sApiKey = "" Then
            MsgBox("Please enter your OpenAI API key in the App.config file.")
            End
        Else
            OPENAI_API_KEY = sApiKey
        End If

        'SetModels()
        cbModel.Items.Clear()
        cbModel.Items.Add("text-davinci-003")
        cbModel.Items.Add("text-davinci-002")
        cbModel.Items.Add("code-davinci-002")

        cbModel.SelectedIndex = 0


        cbVoice.Items.Clear()
        Dim synth As New SpeechSynthesizer()
        For Each voice In synth.GetInstalledVoices()
            cbVoice.Items.Add(voice.VoiceInfo.Name)
        Next
        cbVoice.SelectedIndex = 0

    End Sub

    Private Sub chkListen_CheckedChanged(sender As Object, e As EventArgs) Handles chkListen.CheckedChanged
        If chkListen.Checked Then
            lblSpeech.Text = ""
            lblSpeech.Visible = True
            SpeechToText()
        Else
            oSpeechRecognitionEngine.RecognizeAsyncStop()
            lblSpeech.Visible = False
        End If
    End Sub
    Private Sub chkMute_CheckedChanged(sender As Object, e As EventArgs) Handles chkMute.CheckedChanged

        If chkMute.Checked Then
            lblVoice.Visible = False
            cbVoice.Visible = False
        Else
            lblVoice.Visible = True
            cbVoice.Visible = True
        End If

    End Sub

    Private Sub SpeechToText()

        If oSpeechRecognitionEngine IsNot Nothing Then
            oSpeechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple)
            Exit Sub
        End If

        oSpeechRecognitionEngine = New SpeechRecognitionEngine(New System.Globalization.CultureInfo("en-US"))
        oSpeechRecognitionEngine.LoadGrammar(New DictationGrammar())
        AddHandler oSpeechRecognitionEngine.SpeechRecognized, AddressOf OnSpeechRecognized
        AddHandler oSpeechRecognitionEngine.SpeechHypothesized, AddressOf OnSpeechHypothesized
        oSpeechRecognitionEngine.SetInputToDefaultAudioDevice()
        oSpeechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple)
    End Sub

    Private Sub OnSpeechRecognized(sender As Object, e As SpeechRecognizedEventArgs)
        lblSpeech.Text = "" 'Reset Hypothesized text

        If txtQuestion.Text <> "" Then
            txtQuestion.Text += vbCrLf
        End If

        Dim text As String = e.Result.Text
        txtQuestion.Text += text
    End Sub

    Private Sub OnSpeechHypothesized(sender As Object, e As SpeechHypothesizedEventArgs)
        Dim text As String = e.Result.Text
        lblSpeech.Text = text
    End Sub

    Private Sub btnSend_Click(sender As Object, e As EventArgs) Handles btnSend.Click

        Dim sQuestion As String = txtQuestion.Text
        If sQuestion = "" Then
            MsgBox("Type in your question!")
            txtQuestion.Focus()
            Exit Sub
        End If

        If txtAnswer.Text <> "" Then
            txtAnswer.AppendText(vbCrLf)
        End If

        txtAnswer.AppendText(DateTime.Now.ToString("HH:mm:ss") & " | Me: " & sQuestion & vbCrLf)
        txtQuestion.Text = ""


        Try
            Dim sAnswer As String = SendMsg(sQuestion)
            SpeechToText(sAnswer)
            'txtAnswer.AppendText(DateTime.Now.ToString("HH:mm:ss") & " | GPT: " & sAnswer & vbCrLf)
            'comment the FOR cycle and uncomment the line above to make answer appear without typing effect
            For Each c As Char In DateTime.Now.ToString("HH:mm:ss") & " | GPT: " & sAnswer & vbCrLf
                txtAnswer.AppendText(c)
                Threading.Thread.Sleep(50)
            Next


        Catch ex As Exception
            txtAnswer.AppendText("Error: " & ex.Message)
        End Try

    End Sub

    Sub SpeechToText(ByVal s As String)

        If chkMute.Checked Then
            Exit Sub
        End If

        If oSpeechSynthesizer Is Nothing Then
            oSpeechSynthesizer = New System.Speech.Synthesis.SpeechSynthesizer()
            oSpeechSynthesizer.SetOutputToDefaultAudioDevice()
        End If

        If cbVoice.Text <> "" Then
            oSpeechSynthesizer.SelectVoice(cbVoice.Text)
        End If

        Dim threadSpeechToText As New Threading.Thread(Sub() oSpeechSynthesizer.Speak(s))
        threadSpeechToText.Start()

    End Sub

    Function SendMsg(ByVal sQuestion As String)

        System.Net.ServicePointManager.SecurityProtocol = DirectCast(3072, System.Net.SecurityProtocolType)



        Dim apiEndpoint As String = "https://api.openai.com/v1/completions"
        Dim request As HttpWebRequest = WebRequest.Create(apiEndpoint)
        request.Method = "POST"
        request.ContentType = "application/json"
        request.Headers.Add("Authorization", "Bearer " & OPENAI_API_KEY)

        Dim iMaxTokens As Integer = txtMaxTokens.Text '2048

        Dim dTemperature As Double = TextBox1.Text '0.5 default
        If dTemperature < 0 Or dTemperature > 1 Then
            MsgBox("Randomness has to be between 0 and 1 with higher values resulting in more random text")
            Return ""
        End If

        Dim sUserId As String = "1"
        Dim sModel As String = cbModel.Text 'text-davinci-002, text-davinci-003

        'https://beta.openai.com/docs/api-reference/completions/create
        Dim data As String = "{"
        data += " ""model"":""" & sModel & ""","
        data += " ""prompt"": """ & PadQuotes(sQuestion) & ""","
        data += " ""max_tokens"": " & iMaxTokens & ","
        data += " ""user"": """ & sUserId & """, "
        data += " ""temperature"": " & dTemperature & " "
        data += "}"
        'txtAnswer.Text = data
        Using streamWriter As New StreamWriter(request.GetRequestStream())
            streamWriter.Write(data)
            streamWriter.Flush()
            streamWriter.Close()
        End Using

        Dim response As HttpWebResponse = request.GetResponse()
        Dim streamReader As New StreamReader(response.GetResponseStream())
        Dim sJson As String = streamReader.ReadToEnd()
        'Return sJson

        Dim oJavaScriptSerializer As New System.Web.Script.Serialization.JavaScriptSerializer
        Dim oJson As Hashtable = oJavaScriptSerializer.Deserialize(Of Hashtable)(sJson)
        Dim sResponse As String = oJson("choices")(0)("text")

        Return sResponse

    End Function

    Private Sub SetModels()
        'https://beta.openai.com/docs/models/gpt-3

        'System.Net.ServicePointManager.SecurityProtocol =            System.Net.SecurityProtocolType.Tls Or Ssl3

        System.Net.ServicePointManager.SecurityProtocol = DirectCast(3072, System.Net.SecurityProtocolType)



        Dim apiEndpoint As String = "https://api.openai.com/v1/models"
        Dim request As HttpWebRequest = WebRequest.Create(apiEndpoint)
        request.Method = "GET"
        request.ContentType = "application/json"
        request.Headers.Add("Authorization", "Bearer " & OPENAI_API_KEY)

        Dim response As HttpWebResponse = request.GetResponse()
        Dim streamReader As New StreamReader(response.GetResponseStream())
        Dim sJson As String = streamReader.ReadToEnd()
        'Return sJson

        cbModel.Items.Clear()

        Dim oSortedList As SortedList = New SortedList()
        Dim oJavaScriptSerializer As New System.Web.Script.Serialization.JavaScriptSerializer
        Dim oJson As Hashtable = oJavaScriptSerializer.Deserialize(Of Hashtable)(sJson)
        Dim oList As Object() = oJson("data")
        For i As Integer = 0 To oList.Length - 1
            Dim sId As String = oList(i)("id")
            oSortedList.Add(sId, sId)
        Next

        'Text-davinci - 3
        'Text-davinci - 2
        'code-davinci - 2

        For Each oItem As DictionaryEntry In oSortedList
            cbModel.Items.Add(oItem.Key)
        Next


    End Sub

    Private Function PadQuotes(ByVal s As String) As String

        If s.IndexOf("\") <> -1 Then
            s = Replace(s, "\", "\\")
        End If

        If s.IndexOf(vbCrLf) <> -1 Then
            s = Replace(s, vbCrLf, "\n")
        End If

        If s.IndexOf(vbCr) <> -1 Then
            s = Replace(s, vbCr, "\r")
        End If

        If s.IndexOf(vbLf) <> -1 Then
            s = Replace(s, vbLf, "\f")
        End If

        If s.IndexOf(vbTab) <> -1 Then
            s = Replace(s, vbTab, "\t")
        End If

        If s.IndexOf("""") = -1 Then
            Return s
        Else
            Return Replace(s, """", "\""")
        End If
    End Function

    Private Sub txtQuestion_KeyUp(sender As System.Object, e As System.Windows.Forms.KeyEventArgs) Handles txtQuestion.KeyUp
        If e.KeyCode = Keys.Enter Then
            Dim speed = Val(NumericUpDown1.Value)
            Dim sQuestion As String = txtQuestion.Text
            If sQuestion = "" Then
                MsgBox("Type in your question!")
                txtQuestion.Focus()
                Exit Sub
            End If

            If txtAnswer.Text <> "" Then
                txtAnswer.AppendText(vbCrLf)
            End If

            txtAnswer.AppendText(DateTime.Now.ToString("HH:mm:ss") & " | Me: " & sQuestion & vbCrLf)
            txtQuestion.Text = ""


            Try
                Dim sAnswer As String = SendMsg(sQuestion)
                SpeechToText(sAnswer)
                'txtAnswer.AppendText(DateTime.Now.ToString("HH:mm:ss") & " | GPT: " & sAnswer & vbCrLf)
                'comment the FOR cycle and uncomment the line above to make answer appear without typing effect
                For Each c As Char In DateTime.Now.ToString("HH:mm:ss") & " | GPT: " & sAnswer & vbCrLf
                    txtAnswer.AppendText(c)
                    Threading.Thread.Sleep(speed)
                Next


            Catch ex As Exception
                txtAnswer.AppendText("Error: " & ex.Message)
            End Try
        End If
    End Sub

    Private Sub TextBox1_TextChanged(sender As System.Object, e As System.EventArgs) Handles TextBox1.TextChanged

    End Sub

    Private Sub Button2_Click(sender As System.Object, e As System.EventArgs) Handles Button2.Click
        Dim prefix As String = "vivacityGPTchat_"
        'Dim dateTime As String = dateTime.Now("yyyy-MM-dd HHmmss")
        Dim filename As String = prefix & dateTime.Now.tostring("yyyy-MM-dd HHmmss") & ".txt"

        Using writer As TextWriter = New StreamWriter(filename)
            writer.Write(txtAnswer.Text)
        End Using

        MessageBox.Show("The Chat has been saved to file: " & filename & ".", "Save complete", MessageBoxButtons.OK, MessageBoxIcon.Information)

    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles Button1.Click
        txtAnswer.Clear()
    End Sub

   
End Class
