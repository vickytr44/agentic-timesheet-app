const fs = require('fs');
const path = require('path');

const targets = [
  'node_modules/@copilotkit/react-ui/tsconfig.json',
  'node_modules/@copilotkit/react-textarea/tsconfig.json',
  'node_modules/@copilotkit/runtime-client-gql/tsconfig.json',
  'node_modules/@copilotkit/web-inspector/tsconfig.json'
];

targets.forEach(relPath => {
  const fullPath = path.resolve(__dirname, '..', relPath);
  if (fs.existsSync(fullPath)) {
    try {
      fs.unlinkSync(fullPath);
      console.log(`Successfully deleted broken tsconfig: ${relPath}`);
    } catch (err) {
      console.error(`Error deleting ${relPath}:`, err.message);
    }
  }
});
