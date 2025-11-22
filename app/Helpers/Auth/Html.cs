public static class Html
{
    public const string SuccessInnerHtml =
        """
        <div class="checkmark">✓</div>
        <h1>Kjempemessig!</h1>
        <p>Du kan nå lukke dette vinduet.</p>
        """;

    public const string ErrorInnerHtml =
        """
        <div class="checkmark">✓</div>
        <h1>Auda!</h1>
        <p>Noe gikk galt her gitt.</p>
        """;

      public const string LayoutHtml =
       """
       <!DOCTYPE html>
       <html>
       <head>
           <meta charset="UTF-8">
           <meta name="viewport" content="width=device-width, initial-scale=1.0">
           <title>Thx!</title>
           <style>
               body {
                   font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                   display: flex;
                   justify-content: center;
                   align-items: center;
                   height: 100vh;
                   margin: 0;
                   background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
               }
               .container {
                   background: white;
                   padding: 2rem 3rem;
                   border-radius: 10px;
                   box-shadow: 0 10px 25px rgba(0,0,0,0.2);
                   text-align: center;
               }
               h1 {
                   color: #667eea;
                   margin: 0 0 1rem 0;
                   font-size: 2rem;
               }
               p {
                   color: #666;
                   margin: 0;
                   font-size: 1rem;
               }
               .checkmark {
                   font-size: 3rem;
                   color: #4caf50;
                   margin-bottom: 1rem;
               }
           </style>
       </head>
       <body>
           <div class="container">
            {{InnerHtml}}
           </div>
       </body>
       </html>
       """;
}
