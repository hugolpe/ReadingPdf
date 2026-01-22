// safe predicate for Array.find
const found = arr.find(item => String(item?.someProp).indexOf('needle') !== -1);

async function doWork() {
  try {
    // your async code that may throw
  } catch (err) {
    console.error('contentScript error in doWork:', err, { arrSnapshot: arr });
    throw err; // or handle gracefully
  }
}