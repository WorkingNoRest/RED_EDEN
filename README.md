# RED EDEN — أنظمة اللاعب والأصول الجاهزة

أنظمة RED EDEN والأصول الجاهزة للمخزون والتفاعل والتصنيع والموارد والصومعة الزراعية، معدّة للتركيب على شخصية **Unity Starter Assets — Third Person Controller** مع إبقاء حركة الشخصية الأصلية كما هي.

## محتويات المستودع

- `Assets/RED EDEN/` — مجلد Unity كامل مع ملفات `.meta`، السكربتات، قواعد البيانات الجاهزة، عناصرها، الموارد، المشاهد، النماذج، وprefab الصومعة الزراعية.
- `Assets/Prefabs/` — مجلد Prefabs كامل مع موديلات الموارد والمحطة وملفات المواد والخامات التابعة لها.
- `packages/RedEden-Core.unitypackage` — الحزمة الكاملة للاستيراد مرة واحدة: مجلد Crafting وPrefabs واعتماديات Devion اللازمة، بما فيها نموذج الصومعة وprefab الجاهز.
- `packages/RedEden-Devion-Dependencies.unitypackage` — سكربتات Devion المطلوبة فقط، لاستخدامها عند نسخ مجلدي `Assets/` يدويًا إلى مشروع لا يحتوي Devion بعد.
- `packages/RedEden-ARES-Optional.unitypackage` — طبقة ARES الاختيارية للمساعد/المسح/الصوت والحالة.
- `docs/RedEden-Installation-AR.pdf` — دليل التركيب العربي.
- قواعد البيانات الجاهزة داخل `Assets/RED EDEN/Crafting/Data/`:
  - `RED EDEN Core Inventory Database.asset` — قاعدة Devion الرئيسية وإعداداتها الافتراضية.
  - `RED EDEN Item Database.asset` — عناصر RED EDEN الأربعة عشر، وتُضاف كقاعدة فرعية.
  - `RED EDEN Crafting Database.asset` — تعريفات الموارد والوصفات.

## التوافق والاعتماديات

- إصدار المشروع الذي جُهّزت عليه الحزمة: Unity `6000.3.16f1`.
- فعّل **Input System Package (New)** من `Project Settings > Player > Active Input Handling`.
- تحتاج الأنظمة إلى Devion Inventory System وTriggers وUtilities وUI Widgets. إذا كانت موجودة مسبقًا، احتفظ بنسختك المتوافقة ولا تستورد اعتماديات Devion مرة ثانية.
- واجهة Devion Inventory Window نفسها غير مضمنة؛ استخدم الواجهة من تثبيت Devion المرخّص لديك. هذا يتجنب سحب محتوى الأمثلة أو متحكم Devion للشخصية إلى الحزمة.
- لا تتضمن الحزمة Devion Third Person Controller؛ الحركة والكاميرا تبقيان من Unity Starter Assets.
- عند نسخ مجلد `Assets/RED EDEN` كاملًا، يحتوي أيضًا سكربتات ARES. إذا احتفظت بها، استخدم حزمة ARES الاختيارية واعتماد Unity `com.whisper.unity` المذكورين أدناه؛ أو استبعد مجلد `Assets/RED EDEN/ARES/` إذا لم تكن تحتاجها.
- ARES إضافة اختيارية تعتمد على `com.whisper.unity` وخادم Ollama محلي. لا حاجة إليها لتشغيل المخزون أو التفاعل أو التصنيع.

## التركيب

**نسخ المجلدات الجاهزة:** نزّل المستودع بصيغة ZIP، ثم انسخ `Assets/RED EDEN/` و`Assets/Prefabs/` إلى مجلد `Assets/` في مشروع Unity، مع الحفاظ على بنية المجلدات وجميع ملفات `.meta`. لا تضع المجلدين تحت `Assets/Assets/`. إذا كانت هذه المسارات موجودة في المشروع، خذ نسخة احتياطية وقارن المحتوى قبل الدمج كي لا تستبدل أصولًا محلية.

إذا كانت أنظمة Devion غير مثبتة، استورد `RedEden-Devion-Dependencies.unitypackage` بعد نسخ المجلدين. لا تستورد اعتماديات Devion المكررة إذا كانت موجودة وملائمة. عند الاحتفاظ بـ ARES، أضف اعتماد `com.whisper.unity` من Package Manager (`https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity`) قبل انتظار اكتمال تجميع سكربتات ARES.

**بديل الاستيراد الكامل:** في مشروع نظيف، استورد `RedEden-Core.unitypackage` بدل نسخ مجلدي `Assets/` يدويًا؛ يحتوي الحزمة على أنظمة RED EDEN وPrefabs والاعتماديات.

بعدها:

1. انتظر اكتمال استيراد الملفات وتجميع السكربتات، وعالج أي أخطاء في Console.
2. أضف `InventoryManager` واحدًا إلى المشهد. اجعل `RED EDEN Core Inventory Database.asset` القاعدة الرئيسية و`RED EDEN Item Database.asset` قاعدة فرعية، وأضف قواعد العناصر الأخرى المطلوبة للعبة دون تكرار.
3. أضف واجهة Devion Inventory من تثبيتك المرخّص و`EventSystem` إذا لم يكونا موجودين، واربطهما وفق دليل Devion.
4. أبقِ مكونات حركة وكاميرا Unity Third Person كما هي. تحقق من Tag اللاعب `Player` ومن إعداد Input System.
5. اسحب Prefab المحطة من `Assets/Prefabs/Machines/` وPrefab الصومعة من `Assets/RED EDEN/Crafting/Data/Prefabs/Machines/` إلى المشهد. اسحب عقد الموارد من `Assets/RED EDEN/Crafting/Data/Prefabs/ResourceNodes/` عند الحاجة؛ Prefabs خامات الموارد موجودة في `Assets/Prefabs/Ores/`.
6. راجع دليل PDF لإعداد المكونات والاختبار. ARES اختيارية ولا تستبدل نظام الحركة.

## تنبيهات معروفة

- يتضمن المستودع نموذج `Hydroponic_Farm_M01.fbx` وPrefab الصومعة الجاهز `Hydroponic Farm M01 - Agricultural Silo.prefab` مع مراجع النموذج والعناصر. استخدم الـ prefab الجاهز؛ لا تحتاج إلى تشغيل أداة إنشاء prefab أثناء التركيب المعتاد.
- ملفات Devion من أصول طرف ثالث. استخدمها ووزّعها فقط ضمن صلاحيات وترخيص Devion المطبّقين على مشروعك.
- تضمّن المستودع ملفات اختبارات Unity الخاصة بالتصنيع. شغّل اختبارات `RedEden.Crafting` من Test Runner بعد الاستيراد.

## الحزم

استخدم نسخ المجلدات الجاهزة أو حزمة Core الكاملة، وليس كليهما في عملية تركيب واحدة. استورد ARES الاختيارية فقط إذا كان المشروع يحتاج المساعد أو الماسح أو إدخال الصوت؛ إعداد ARES وخدماته المحلية موضّحان في PDF.
