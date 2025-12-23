const { getDefaultConfig, mergeConfig } = require('@react-native/metro-config');
const path = require('path');

/**
 * Metro configuration
 * https://reactnative.dev/docs/metro
 *
 * @type {import('@react-native/metro-config').MetroConfig}
 */
const defaultConfig = getDefaultConfig(__dirname);

const mobileNodeModules = path.resolve(__dirname, 'node_modules');

const config = {
  watchFolders: [
    path.resolve(__dirname, '..'),
  ],
  resolver: {
    ...defaultConfig.resolver,
    // Force all module resolution to use mobile's node_modules
    // This ensures files in contracts/ can resolve dependencies from mobile/node_modules
    nodeModulesPaths: [
      mobileNodeModules,
    ],
    // Explicitly map @babel/runtime to mobile's node_modules
    extraNodeModules: {
      '@babel/runtime': path.join(mobileNodeModules, '@babel/runtime'),
    },
  },
  projectRoot: __dirname,
};

module.exports = mergeConfig(defaultConfig, config);
