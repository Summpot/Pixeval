class AppAssets {
  const AppAssets._();

  static const String logo = 'assets/logo.png';
  static const String logo192 = 'assets/logo-192.png';
  static const String logoSvg = 'assets/logo.svg';
  static const String logoIco = 'assets/logo.ico';
  static const String blockedContent = 'assets/blocked-content.png';
  static const String imageNotAvailable = 'assets/image-not-available.png';

  // Platform Icons
  static const String platformDanbooru = 'assets/platforms/danbooru.png';
  static const String platformGelbooru = 'assets/platforms/gelbooru.png';
  static const String platformPixiv = 'assets/platforms/pixiv.png';
  static const String platformRule34 = 'assets/platforms/rule34.png';
  static const String platformSankaku = 'assets/platforms/sankaku.png';
  static const String platformSauceNao = 'assets/platforms/saucenao.png';
  static const String platformYandere = 'assets/platforms/yandere.png';

  // SVG Icons
  static const String iconGitHub = 'assets/icons/GitHub.svg';
  static const String iconMcp = 'assets/icons/MCP.svg';
  static const String iconPixiv = 'assets/icons/pixiv.svg';
  static const String iconQq = 'assets/icons/QQ.svg';
  static const String iconSauceNao = 'assets/icons/SauceNAO.svg';

  static String? platformIcon(String platform) {
    switch (platform.toLowerCase()) {
      case 'danbooru':
        return platformDanbooru;
      case 'gelbooru':
        return platformGelbooru;
      case 'pixiv':
        return platformPixiv;
      case 'rule34':
        return platformRule34;
      case 'sankaku':
        return platformSankaku;
      case 'saucenao':
        return platformSauceNao;
      case 'yandere':
        return platformYandere;
      default:
        return null;
    }
  }
}
