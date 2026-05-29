using RadioE45.Logic;

namespace RadioE45.Views;

public partial class MainPage : TabbedPage
{

    public MainPage()
    {
        InitializeComponent();

        SetupRadioPages();
    }
    private void SetupRadioPages()
    {
        Children.Add(new AboutMePage() { Title = "About" });
        Children.Add(new RadioPage(new RadioStation("Radio E45", "https://radioe45.ddns.net:8060/radio.mp3", "galgalatz.png", "#60339B", "#DFB12F")) { Title = "Radio E45" });
        Children.Add(new RadioPage(new RadioStation("Radio ANTANI", "https://radioe45.ddns.net:8000/radio.mp3", "kan_bet.png", "#000000", "#00f7ff")) { Title = "Radio ANTANI" });
        Children.Add(new RadioPage(new RadioStation("Tecno Agency", "https://radioe45.ddns.net:8020/radio.mp3", "kan_88.png","#552586","#B589D6")) { Title = "Tecno Agency" });
        Children.Add(new RadioPage(new RadioStation("Old Bridge 2", "https://radioe45.ddns.net:8040/radio.mp3", "galey_israel.png", "#30c4ff", "#fddb98")) { Title = "Old Bridge 2" });
        //Children.Add(new RadioPage(new RadioStation("Old Bridge 3", "https://radioe45.ddns.net:8050/radio.mp3.", "radio_darom.jpeg", "#2c67f2", "#62cff4")) { Title = "Old Bridge 3" });
      
        CurrentPage = Children[1];
    }
}
