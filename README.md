# RED EDEN — أنظمة اللاعب الجاهزة

حزمة Unity موحّدة لأنظمة RED EDEN: المخزون والتفاعل والتصنيع والموارد والصومعة الزراعية، مع ARES المتصل بـ Gemini Live. صُممت لتُركّب على شخصية **Unity Starter Assets — Third Person Controller** دون استبدال حركة اللاعب أو الكاميرا.

## الحزمة الرئيسية

استورد [RedEden-Full-System.unitypackage](./packages/RedEden-Full-System.unitypackage) من **Assets > Import Package > Custom Package**. تشمل الحزمة الملفات والأصول المملوكة للمشروع، ومنها:

- موديل `Hydroponic_Farm_M01.fbx` وPrefab الصومعة الزراعية الجاهز.
- قواعد بيانات العناصر والتصنيع والمخزون، وPrefabs الموارد والمحطات.
- سكربتات RED EDEN والتفاعل، وطبقة ARES Gemini Live وملفاتها `.meta`.
- لا تشمل مشهد اللعبة الأصلي أو شخصية اللاعب أو ملفات Unity Starter Assets.
- لا تتضمن كود Devion أو واجهاته أو أمثلته؛ يجب تثبيت Devion من الحساب المرخص للمشروع.
- لا تتضمن Whisper أو Ollama أو أدوات تنزيل وتشغيل نماذج محلية.

المصادر والبيانات الجاهزة موجودة في `Assets/RED EDEN/` و`Assets/Prefabs/`. راجع [قائمة الاعتماديات والتراخيص](./DEPENDENCIES.md) و[دليل التركيب بالعربية PDF](./docs/RedEden-Installation-Guide-AR.pdf) أو [English PDF](./docs/RedEden-Installation-Guide-EN.pdf).

## المتطلبات والتركيب

- Unity `6000.3.16f1` أو إصدار Unity 6 متوافق.
- Unity Input System `1.19.0` وURP `17.3.0` أو إصدارات متوافقة.
- وحدات Devion المرخصة: Inventory System وStat System وTriggers وUtilities وUI Widgets، مثبتة مسبقًا في المشروع الهدف؛ رقم إصدار Devion غير موجود في بيانات الملفات المصدرية.
- دمج اعتمادي Input System وURP المحددين في `ARES_INTEGRATION_PACKAGE/Unity/manifest-dependencies.json` مع `Packages/manifest.json` الموجود؛ لا تستبدل ملف manifest كاملًا.
- أبقِ Unity Third Person Controller والكاميرا كما هما. أضف InventoryManager واحدًا، عيّن قاعدة RED EDEN الأساسية وقاعدة العناصر التابعة، واستخدم واجهة Devion من التثبيت المرخص.

لخطوات الإعداد التفصيلية، راجع [دليل التركيب](./ARES_INTEGRATION_PACKAGE/INSTALLATION_GUIDE.md) و[قائمة التحقق](./INSTALLATION-CHECKLIST.md).

## ARES Gemini Live

يرسل ARES الصوت عبر WebSocket إلى جسر خلفي متصل بـ Gemini Live؛ اضغط باستمرار **V** للتحدث. صوت الميكروفون PCM أحادي 16 kHz، وصوت الرد PCM 24 kHz. مفتاح Gemini ومفتاح توقيع الجلسات يبقيان على الخادم ولا يوضعان في Unity.

الكود الخلفي في `ARES_INTEGRATION_PACKAGE/Backend/`. فرع `main` يحتوي تنفيذ WebSocket الحقيقي، لكن الخدمة العامة الموجودة قديمة وتعرض حاليًا مسارات HTTP فقط. يلزم نشر نسخة `render.yaml` وضبط `GEMINI_API_KEY` في لوحة Render قبل اعتبار الاتصال الحي جاهزًا. لا تُعدّ استجابة `/health` اختبارًا لصوت Gemini.

مصادر ARES:
- [حزمة التكامل](./ARES_INTEGRATION_PACKAGE/README.md)
- [إعداد Gemini Live](./ARES_INTEGRATION_PACKAGE/Documentation/ARES_GEMINI_LIVE_SETUP.md)
- [ملاحظات الأمان](./ARES_INTEGRATION_PACKAGE/Documentation/ARES_ARCHITECTURE.md)
- [تعليمات النشر](./ARES_INTEGRATION_PACKAGE/INSTALLATION_GUIDE.md)

## التحقق

- Unity `6000.3.16f1`: استيراد الحزمة وتجميعها في مشروع تحقق نظيف مع تثبيت Devion المرخص محليًا؛ اجتازت اختبارات ARES التسعة واختبارات Crafting الستة.
- تدقيق الحزمة: 307 مسارات أصول، تشمل الموديل والـPrefab وقواعد البيانات؛ دون مشاهد أو ملفات Devion أو Starter Assets.
- اختبارات Python الخلفية: 27 اختبارًا ناجحًا تشمل الجلسات والاتصال والبروتوكول وحدود الرسائل والأدوات.
- لم يُجرَ اتصال Gemini حي من دون مفتاح صالح، ولم يُتحقق من صوت جهاز حقيقي.

لا يمنح المستودع ترخيصًا لأصول الطرف الثالث. راجع [الإشعارات](./LICENSE-AND-THIRD-PARTY-NOTICES.md) قبل إعادة التوزيع.
