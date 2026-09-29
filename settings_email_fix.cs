private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(10);

                var payload = new
                {
                    _subject = subject,
                    message = body,
                    _captcha = "false",
                    _template = "table"
                };

                var response = await client.PostAsJsonAsync($"https://formsubmit.co/ajax/{Uri.EscapeDataString(toEmail)}", payload);
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] FormSubmit ile başarıyla gönderildi: {toEmail}");
                    return;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] FormSubmit hata: {response.StatusCode} - {errorResponse}");
                    throw new Exception($"FormSubmit servis hatası: {response.StatusCode} - {errorResponse}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] Exception: {ex.Message}");
            throw new Exception($"E-posta gönderim hatası: {ex.Message}");
        }
    }