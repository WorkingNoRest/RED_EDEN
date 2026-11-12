# RED EDEN — أنظمة اللاعب

حزمة أنظمة RED EDEN الخاصة بالمخزون والتفاعل والتصنيع والصومعة الزراعية، معدّة للتركيب على شخصية **Unity Starter Assets — Third Person Controller** مع إبقاء حركة الشخصية الأصلية كما هي.

## محتويات المستودع

- `packages/RedEden-Core.unitypackage` — نظام المخزون والتفاعل والتصنيع والموارد والصومعة، مع ملفات Devion اللازمة لتشغيلها.
- `packages/RedEden-ARES-Optional.unitypackage` — طبقة ARES الاختيارية للمساعد/المسح/الصوت والحالة.
- `docs/RedEden-Installation-AR.pdf` — دليل التركيب العربي.
- قاعدة `RED EDEN Core Inventory Database` أساسية نظيفة، منفصلة عن قاعدة عناصر اللعبة.

## التوافق والاعتماديات

- إصدار المشروع الذي جُهّزت عليه الحزمة: Unity `6000.3.16f1`.
- فعّل **Input System Package (New)** من `Project Settings > Player > Active Input Handling`.
- تحتاج حزمة Core إلى أنظمة Devion Inventory System وTriggers وUtilities وUI Widgets المرفقة. إذا كانت هذه الأنظمة مثبتة مسبقًا، احتفظ بنسختك المتوافقة ولا تستورد مجلدات Devion المكررة من الحزمة.
- واجهة Devion Inventory Window نفسها غير مضمنة؛ استخدم الواجهة من تثبيت Devion المرخّص لديك. هذا يتجنب سحب محتوى الأمثلة أو متحكم Devion للشخصية إلى الحزمة.
- لا تتضمن الحزمة Devion Third Person Controller؛ الحركة والكاميرا تبقيان من Unity Starter Assets.
- ARES إضافة اختيارية؛ تعتمد على حزمة `com.whisper.unity` وعلى خادم Ollama محلي. لا حاجة إلى تثبيتها لتشغيل المخزون أو التفاعل أو التصنيع.

## تركيب سريع

1. افتح نسخة احتياطية من مشروع Unity، واستورد `RedEden-Core.unitypackage`.
2. أضف `InventoryManager` واحدًا إلى مشهد اللعب، وعيّن `RED EDEN Core Inventory Database.asset` كقاعدة رئيسية، ثم أضف `RED EDEN Item Database.asset` كقاعدة فرعية. القاعدة الفرعية تحتوي العناصر ولا تحتوي إعدادات Devion الأساسية.
3. أضف واجهة Devion Inventory من تثبيتك المرخّص و`EventSystem` إلى المشهد، ثم اربط واجهة المخزون وفق دليل Devion.
4. اترك مكونات حركة Unity Third Person كما هي. تأكد أن كائن اللاعب يحمل Tag باسم `Player` وأنه يستخدم Input System.
5. أضف الصومعة ومحطات التصنيع وعقد الموارد إلى المشهد، واضبط قاعدة التصنيع والموارد من الـ Inspector.
6. راجع دليل PDF قبل تركيب ARES؛ فهي وحدة اختيارية ولا تستبدل نظام الحركة.

## تنبيهات معروفة

- ملف نموذج الصومعة `Hydroponic_Farm_M01.fbx` في المشروع الأصلي أبلغ Unity عن تلف أثناء الاستيراد. أُخرج النموذج والـ prefab المعتمد عليه من حزمة Core؛ أعد تصدير النموذج من مصدره ثم شغّل **Mars > Machines > Create Hydroponic Farm** لتوليد prefab جديد قبل اعتماد الشكل النهائي.
- ملفات Devion من أصول طرف ثالث. استخدمها ووزّعها فقط ضمن صلاحيات وترخيص Devion المطبّقين على مشروعك.
- تضمّن المستودع ملفات اختبارات Unity الخاصة بالتصنيع. شغّل اختبارات `RedEden.Crafting` من Test Runner بعد الاستيراد.

## الحزم

استورد Core أولًا. استورد ARES الاختيارية فقط إذا كان المشروع يحتاج المساعد أو الماسح أو إدخال الصوت؛ إعداد ARES وخدماته المحلية موضّحان في PDF.
