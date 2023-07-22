using System.Linq;
using System.Collections.Generic;
using Soneta.Business;
using Soneta.Business.UI;
using Soneta.CRM;
using Soneta.Handel;
using SonetaAddon;
using Soneta.Magazyny.Dostawy;

[assembly: Worker(typeof(GenerujZamowienieWorker), typeof(DokumentHandlowy))]
namespace SonetaAddon
{
    /// <summary>
    /// Klasa reprezentująca przycisk do generowania dokumentów ZD na podstawie dokumentu ZO
    /// </summary>
    public class GenerujZamowienieWorker
    {
        /// <summary>
        /// Pobiera lub ustawia obiekt dokumentu źródłowego
        /// </summary>
        [Context]
        public DokumentHandlowy Dokument { get; set; }

        /// <summary>
        /// Pobiera lub ustawia obiekt sesji
        /// </summary>
        [Context]
        public Session Session { get; set; }

        /// <summary>
        /// Pobiera obiekt modułu Handel
        /// </summary>
        public HandelModule HandelModule
        {
            get
            {
                if (handelModule == null)
                {
                    handelModule = Session.GetHandel();
                }

                return handelModule;
            }
        }

        /// <summary>
        /// Obiekt modułu Handel
        /// </summary>
        private HandelModule handelModule;

        /// <summary>
        /// Zdrzenie kliknięcia przycisku 'Generuj zamówienia do dostawców'
        /// </summary>
        /// <returns>Okno z komunikatem</returns>
        [Action("Generuj zamówienia do dostawców",
            Mode = ActionMode.OnlyForm,
            Target = ActionTarget.ToolbarWithText,
            Icon = ActionIcon.Organizer,
            Priority = 1)]
        public MessageBoxInformation Generuj()
        {
            IEnumerable<IGrouping<Kontrahent, PozycjaDokHandlowego>> dostawcy = Dokument.Pozycje.GroupBy(x => x.Towar.Dostawca);

            using (ITransaction transaction = Session.Logout(true))
            {
                StanDokumentuHandlowego stanPrzedOperacja = Dokument.Stan;
                Dokument.Stan = StanDokumentuHandlowego.Bufor;

                foreach (IGrouping<Kontrahent, PozycjaDokHandlowego> dostawca in dostawcy)
                {
                    GenerujZamowienieZD(dostawca.Key, dostawca.ToList());
                }

                Dokument.Stan = stanPrzedOperacja;
                transaction.CommitUI();
            }

            return new MessageBoxInformation("Utworzono dokumenty")
            {
                Text = $"Skutecznie utworzono {dostawcy.ToList().Count} dokumentów ZD"
            };
        }

        /// <summary>
        /// Metoda określająca widoczność przycisku
        /// </summary>
        /// <param name="context">Kontekst danych</param>
        /// <returns>Informacja o widoczności przycisku</returns>
        public static bool IsVisible(Context context)
        {
            DokumentHandlowy dokument;
            context.Get(out dokument);
            return dokument.Definicja.Symbol == "ZO";
        }

        /// <summary>
        /// Metoda generująca dokument zamówienia
        /// </summary>
        /// <param name="dostawca">Obiekt dostawcy</param>
        /// <param name="pozycjeZO">Lista pozycji zamówienia</param>
        private void GenerujZamowienieZD(Kontrahent dostawca, List<PozycjaDokHandlowego> pozycjeZO)
        {
            DokumentHandlowy zamowienie = new DokumentHandlowy();
            HandelModule.DokHandlowe.AddRow(zamowienie);

            zamowienie.Definicja = HandelModule.DefDokHandlowych.ZamówienieDostawcy;
            zamowienie.Kontrahent = dostawca;
            zamowienie.Magazyn = Dokument.Magazyn;
            RelacjaHandlowa.Kopiowania relacja = StworzeRelacjeDokumentow(Dokument, zamowienie);

            foreach (PozycjaDokHandlowego pozycjaZO in pozycjeZO)
            {
                PozycjaDokHandlowego pozycjaZD = new PozycjaDokHandlowego(zamowienie);
                HandelModule.PozycjeDokHan.AddRow(pozycjaZD);
                pozycjaZD.Towar = pozycjaZO.Towar;
                pozycjaZD.Ilosc = pozycjaZO.Ilosc;
                StworzRelacjePozycji(relacja, pozycjaZO, pozycjaZD);
            }
        }

        /// <summary>
        /// Metoda tworząca relację pomiędzy dokumentami
        /// </summary>
        /// <param name="nadrzedny">Obiekt dokumentu nadrzędnego</param>
        /// <param name="podrzedny">Obiekt dokumentu podrzędnego</param>
        /// <returns>Obiekt relacji</returns>
        private RelacjaHandlowa.Kopiowania StworzeRelacjeDokumentow(DokumentHandlowy nadrzedny, DokumentHandlowy podrzedny)
        {
            SubTable<DefRelacjiHandlowej> definicje = HandelModule.DefRelHandlowych.WgDefinicjaNadrzednego[nadrzedny.Definicja];
            DefRelacjiHandlowej definicja = definicje.FirstOrDefault(x => x.DefinicjaPodrzednego == podrzedny.Definicja);
            RelacjaHandlowa.Kopiowania relacja = new RelacjaHandlowa.Kopiowania(definicja, nadrzedny, podrzedny);
            HandelModule.RelacjeHandlowe.AddRow(relacja);
            return relacja;
        }

        /// <summary>
        /// Metoda tworząca relację pomiędzy pozycjami
        /// </summary>
        /// <param name="relacja">Obiekt relacji</param>
        /// <param name="pozycjaNadrzedna">Obiekt pozycji nadrzędnej</param>
        /// <param name="pozycjaPodrzedna">Obiekt pozycji podrzędnej</param>
        private void StworzRelacjePozycji(
            RelacjaHandlowa.Kopiowania relacja,
            PozycjaDokHandlowego pozycjaNadrzedna,
            PozycjaDokHandlowego pozycjaPodrzedna)
        {
            PozycjaRelacjiHandlowej pozycja = new PozycjaRelacjiHandlowej(relacja, pozycjaNadrzedna, pozycjaPodrzedna, false);
            HandelModule.PozRelHandlowej.AddRow(pozycja);
        }
    }
}
