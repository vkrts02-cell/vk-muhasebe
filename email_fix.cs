private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var profil = await _dbService.GetFirmaProfiliAsync();
        bool smtpConfigured = profil != null && !string.IsNullOrWhiteSpace(profil.SmtpUser) && !string.IsNullOrWhiteSpace(profil.SmtpPass);
        
        // ÖNCE FormSubmit'i dene (daha güvenilir, hızlı, rate limit yok)
        // SMTP ayarları yüklenmemiş olabilir veya yanlış olabilir
        var formSubmitSuccess = await TryFormSubmitAsync(toEmail, subject, body);
        if (formSubmitSuccess)
        {
            System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit ile başarıyla gönderildi: {toEmail}");
            return;
        }

        // FormSubmit başarısızsa VE SMTP yapılandırılmışsa SMTP dene
        if (smtpConfigured)
        {
            try
            {
                var host = !string.IsNullOrWhiteSpace(profil.SmtpHost) ? profil.SmtpHost : "smtp.gmail.com";
                var port = profil.SmtpPort > 0 ? profil.SmtpPort : 587;
                
                using (var smtp = new System.Net.Mail.SmtpClient(host, port))
                {
                    smtp.EnableSsl = profil.SmtpSsl;
                    smtp.Credentials = new System.Net.NetworkCredential(profil.SmtpUser.Trim(), profil.SmtpPass.Trim());
                    smtp.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                    smtp.Timeout = 8000; // Kısaltıldı: 15sn -> 8sn

                    using (var msg = new System.Net.Mail.MailMessage())
                    {
                        msg.From = new System.Net.Mail.MailAddress(profil.SmtpUser.Trim(), "VK Ön Muhasebe");
                        msg.To.Add(toEmail.Trim());
                        msg.Subject = subject;
                        msg.Body = body;
                        msg.IsBodyHtml = false;

                        await smtp.SendMailAsync(msg);
                        System.Diagnostics.Debug.WriteLine($"[SendEmail] SMTP ile başarıyla gönderildi: {toEmail}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendEmail] SMTP Hatası: {ex.Message}");
                // SMTP de başarısız
            }
        }

        // Her ikisi de başarısızsa hata fırlat
        throw new Exception("E-posta gönderilemedi: FormSubmit ve SMTP (varsa) denendi ama başarısız oldu. Lütfen SMTP ayarlarınızı kontrol edin veya daha sonra tekrar deneyin.");
    }

    private async Task<bool> TryFormSubmitAsync(string toEmail, string subject, string body)
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
                    return true;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit hata: {response.StatusCode} - {errorResponse}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit exception: {ex.Message}");
            return false;
        }
    }