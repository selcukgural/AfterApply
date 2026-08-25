import Link from "next/link";

export default function PrivacyPage() {
  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900">Gizlilik Politikası</h1>
      <p className="mb-8 text-sm text-gray-500">Son güncelleme: 2026</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700">
        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900">Hangi veriyi topluyoruz ve neden</h2>
          <p>
            AfterApply, iş başvurularınızı takip etmenize yardımcı olan kişisel bir araçtır. Hizmeti
            sağlayabilmek için şu verileri işleriz:
          </p>
          <ul className="mt-2 list-disc pl-5">
            <li>Hesap bilgileri: ad, soyad, e-posta adresi, şifre (hash&apos;lenmiş olarak saklanır).</li>
            <li>
              Girdiğiniz veya içe aktardığınız başvuru verileri: şirket, iş ilanı, başvuru durumu, tarihler
              ve notlar.
            </li>
            <li>Bu verilerden türetilen hatırlatmalar (takip önerileri, yanıtsız başvuru uyarıları).</li>
          </ul>
          <p className="mt-2">
            Bu veriler yalnızca size başvuru takibi ve kişisel analiz özelliklerini sunmak için kullanılır.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900">Saklama süresi</h2>
          <p>
            Verileriniz hesabınızı silene kadar saklanır. Otomatik bir süre sonunda silinme
            uygulanmamaktadır.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900">Haklarınız</h2>
          <p>
            Verilerinizin bir kopyasını istediğiniz zaman dışa aktarabilir veya hesabınızı kalıcı olarak
            silebilirsiniz. Bu işlemleri{" "}
            <Link href="/settings" className="text-blue-600 hover:underline">
              Hesap Ayarları
            </Link>{" "}
            sayfasından yapabilirsiniz. Hesap silme işlemi, hesabınıza ait tüm başvuru, import ve
            hatırlatma verilerini kalıcı olarak kaldırır.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900">Şu an geçerli olmayanlar</h2>
          <p>
            AfterApply henüz e-posta entegrasyonu veya herkese açık/şirket bazlı analitik özellikleri
            sunmamaktadır. Bu nedenle e-posta izin sınırları ve anonimleştirme/minimum örneklem
            kontrolleri şu an için uygulanabilir değildir — bu özellikler eklendiğinde bu politika
            güncellenecektir.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900">İletişim</h2>
          <p>
            Sorularınız için{" "}
            <a href="mailto:privacy@afterapply.app" className="text-blue-600 hover:underline">
              privacy@afterapply.app
            </a>{" "}
            adresinden bize ulaşabilirsiniz.
          </p>
        </section>
      </div>
    </div>
  );
}
